using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Threading.Tasks;

namespace OpenMirror {
    [DataContract]
    public sealed class Settings {
        [DataMember] public string ReceiverName = "OpenMirror - " + Environment.MachineName;
        [DataMember] public string Resolution = "1920x1080";
        [DataMember] public int Fps = 30;
        [DataMember] public bool Audio = true;
        [DataMember] public bool RequirePin = true;
        [DataMember] public string Pin = CreatePin();
        [DataMember] public string SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "OpenMirror");
        [DataMember] public string Microphone = "";
        [DataMember] public string DevicePrefix = CreateDevicePrefix();
        [DataMember] public string Language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="it"?"it":"en";
        [DataMember] public bool AutoReconnect = true;
        [DataMember] public bool SoftwareDecoder;
        [DataMember] public bool StretchVideo;
        [DataMember] public int SchemaVersion=2;
        public static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenMirror");
        public static string FilePath { get { return Path.Combine(DataFolder, "settings.json"); } }
        public static string CreatePin() { var b = new byte[4]; using (var r = RandomNumberGenerator.Create()) r.GetBytes(b); return (1 + BitConverter.ToUInt32(b,0) % 9999).ToString("0000"); }
        static string CreateDevicePrefix(){var b=new byte[4];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return "02:"+string.Join(":",b.Select(x=>x.ToString("X2")));}
        public static Settings Load() {
            try { using (var f = File.OpenRead(FilePath)) {
                var s = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f);
                if(s.SchemaVersion<2){s.AutoReconnect=true;s.SchemaVersion=2;}
                if (string.IsNullOrWhiteSpace(s.ReceiverName) || string.IsNullOrWhiteSpace(s.SaveFolder)) return new Settings();
                if (!new[]{"1280x720","1920x1080","2560x1440","3840x2160"}.Contains(s.Resolution)) s.Resolution="1920x1080";
                if(s.Language!="it"&&s.Language!="en")s.Language="it";
                if (!new[]{15,30,60}.Contains(s.Fps)) s.Fps=30;
                if (!System.Text.RegularExpressions.Regex.IsMatch(s.Pin ?? "", "^[0-9]{4}$") || s.Pin=="0000") s.Pin=CreatePin();
                if (!System.Text.RegularExpressions.Regex.IsMatch(s.DevicePrefix??"","^02(:[0-9A-F]{2}){4}$"))s.DevicePrefix=CreateDevicePrefix();
                return s;
            }} catch { return new Settings(); }
        }
        public void Save() {
            Directory.CreateDirectory(DataFolder);
            var tmp = FilePath + ".tmp";
            using (var f = File.Create(tmp)) new DataContractJsonSerializer(typeof(Settings)).WriteObject(f,this);
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null); else File.Move(tmp,FilePath);
        }
    }
    public static class Paths {
        public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
        public static string Airplay {get{return Path.Combine(Root,"bin","airplay","uxplay.exe");}}
        public static string Android {get{return Path.Combine(Root,"bin","android","scrcpy.exe");}}
        public static string Adb {get{return Path.Combine(Root,"bin","android","adb.exe");}}
        public static string Ffmpeg {get{return Path.Combine(Root,"bin","ffmpeg","ffmpeg.exe");}}
        public static string Output(Settings s,string ext) {
            Directory.CreateDirectory(s.SaveFolder);
            return Path.Combine(s.SaveFolder,"OpenMirror-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+ext);
        }
        public static void Open(string path) { Process.Start(new ProcessStartInfo(path) {UseShellExecute=true}); }
    }
    public sealed class Session : IDisposable {
        public string Name, Protocol="Demo", Serial, State=Loc.T("Avvio…","Starting…"), LastLine="";
        public int Slot;
        public IntPtr Video;
        public volatile bool Stopping, Demo;
        public event Action Changed;
        public event Action<string> Log;
        readonly object gate = new object();
        readonly object callbackGate = new object();
        readonly Queue<string> lines = new Queue<string>();
        Process owned;
        SafeFileHandle job;
        Task stopTask;
        volatile bool disposed;
        int? exitCode;
        DateTime startedAt;
        long generation;
        const int MaximumLogLines=120, MaximumLineLength=2048;
        public Process Process {get{lock(gate)return owned;}}
        public int? ExitCode {get{lock(gate){var value=ReadExitCode(owned);if(value.HasValue)exitCode=value;return exitCode;}}}
        public DateTime StartedAt {get{lock(gate)return startedAt;}}
        internal bool OwnsProcessTree {get{lock(gate)return job!=null&&!job.IsClosed;}}
        public string Diagnostic {get{lock(gate) return string.Join(Environment.NewLine,lines.ToArray());}}
        public bool Alive {get{lock(gate)return !disposed&&!Stopping&&(Demo||IsAlive(owned));}}
        public bool Connected {get{lock(gate)return !disposed&&!Stopping&&(Demo||Video!=IntPtr.Zero&&Native.IsWindow(Video));}}
        public static string Quote(string value) {return Recorder.QuoteArgument(value);}
        public static string AirplayArguments(Settings s,int slot) {
            var mac=s.DevicePrefix+":"+slot.ToString("X2");
            var name=s.ReceiverName+(slot==1 ? "" : " "+slot);
            return "-n "+Quote(name)+" -nh -p 7100 -m "+mac+" -s "+s.Resolution+"@"+s.Fps+" -fps "+s.Fps+" -vs "+Quote("d3d11videosink force-aspect-ratio="+(s.StretchVideo?"false":"true"))+" -nofreeze -reset 12"+(s.SoftwareDecoder?" -avdec":"")+(s.Audio?"":" -as 0")+(s.RequirePin?" -pin "+s.Pin:"");
        }
        public void Start(string executable,string args) {
            lock(gate) {
                if(disposed)throw new ObjectDisposedException("Session",Loc.T("La sessione è stata chiusa.","The session has been closed."));
                if(IsAlive(owned)||(stopTask!=null&&!stopTask.IsCompleted))throw new InvalidOperationException(Loc.T("Disconnetti il dispositivo prima di avviarne un altro.","Disconnect the device before starting another one."));
                ReleaseProcess();
                lines.Clear();LastLine="";exitCode=null;Stopping=false;Video=IntPtr.Zero;generation++;
                if(Demo||string.Equals(Protocol,"Demo",StringComparison.OrdinalIgnoreCase)) {
                    Demo=true;startedAt=DateTime.Now;State=Loc.T("Dimostrazione","Demo");
                } else {
                    if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable))throw new FileNotFoundException(Loc.T("Il componente di ricezione non è presente. Ripristina i file dell'applicazione.","The receiver component is missing. Restore the application files."),executable);
                    executable=Path.GetFullPath(executable);
                    var si=BuildStartInfo(executable,args??"");
                    var p=new Process{StartInfo=si,EnableRaisingEvents=true};
                    p.OutputDataReceived+=OnProcessLine;p.ErrorDataReceived+=OnProcessLine;p.Exited+=OnExited;
                    owned=p;
                    State=Loc.T("In attesa del dispositivo","Waiting for the device");
                    try {
                        if(!p.Start())throw new InvalidOperationException(Loc.T("Il processo non è stato avviato.","The process did not start."));
                        startedAt=DateTime.Now;job=CreateOwnedJob(p);
                        p.BeginOutputReadLine();p.BeginErrorReadLine();
                    } catch(Exception ex) {
                        owned=null;Detach(p);
                        try{if(IsAlive(p))p.Kill();}catch{}
                        if(job!=null){job.Dispose();job=null;}
                        p.Dispose();State=Loc.T("Avvio non riuscito","Startup failed");
                        throw new InvalidOperationException(Loc.T("Impossibile avviare il ricevitore. Verifica i file dell'applicazione.","Could not start the receiver. Check the application files."),ex);
                    }
                }
            }
            Notify();
        }
        public async Task StartVerifiedAsync(string executable,string args) {
            Start(executable,args);
            long attempt;bool demo;
            lock(gate){attempt=generation;demo=Demo;}
            if(demo)return;
            await Task.Delay(1000).ConfigureAwait(false);
            lock(gate) {
                if(disposed||Stopping||generation!=attempt)throw new OperationCanceledException(Loc.T("Avvio annullato.","Startup cancelled."));
                if(IsAlive(owned))return;
                var value=ReadExitCode(owned);if(value.HasValue)exitCode=value;
                throw new InvalidOperationException(Loc.T("Il ricevitore si è chiuso durante l'avvio","The receiver closed during startup")+(exitCode.HasValue?" ("+exitCode.Value+")":"")+(string.IsNullOrEmpty(LastLine)?".":": "+LastLine));
            }
        }
        ProcessStartInfo BuildStartInfo(string executable,string args) {
            Directory.CreateDirectory(Settings.DataFolder);
            var si=new ProcessStartInfo(executable,args) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(executable),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            si.EnvironmentVariables["PATH"]=Path.GetDirectoryName(executable)+";"+Environment.GetEnvironmentVariable("PATH");
            if (string.Equals(Protocol,"AirPlay",StringComparison.OrdinalIgnoreCase)) {
                var plugins=Path.Combine(Path.GetDirectoryName(executable),"lib","gstreamer-1.0");
                if (!Directory.Exists(plugins)) plugins=Path.Combine(Path.GetDirectoryName(executable),"plugins");
                string[] required={"coreelements","typefindfunctions","app","playback","autodetect","audioconvert","audioresample","volume","level","videoconvertscale","isomp4","rtpmanager","rtp","udp","tcp","videoparsersbad","libav","d3d11","wasapi","audioparsers"};
                var missing=required.Select(x=>"libgst"+x+".dll").Where(x=>!File.Exists(Path.Combine(plugins,x))).ToList();
                string[] libraries={"libgstreamer-1.0-0.dll","libgstapp-1.0-0.dll","libgstaudio-1.0-0.dll","libgstvideo-1.0-0.dll","libglib-2.0-0.dll","libgobject-2.0-0.dll","libcrypto-3-x64.dll","libplist-2.0.dll","avcodec-63.dll","avutil-61.dll","libgcc_s_seh-1.dll","libstdc++-6.dll","libwinpthread-1.dll"};
                missing.AddRange(libraries.Where(x=>!File.Exists(Path.Combine(Path.GetDirectoryName(executable),x))));
                var scanner=Path.Combine(Path.GetDirectoryName(executable),"libexec","gstreamer-1.0","gst-plugin-scanner.exe");
                if(!File.Exists(scanner))scanner=Path.Combine(Path.GetDirectoryName(executable),"gst-plugin-scanner.exe");
                if(!File.Exists(scanner))missing.Add("gst-plugin-scanner.exe");
                if(missing.Count>0)throw new FileNotFoundException(Loc.T("AirPlay non può avviarsi: mancano componenti multimediali. Ripristina l'applicazione.","AirPlay cannot start: media components are missing. Restore the application.")+Environment.NewLine+string.Join(", ",missing));
                si.EnvironmentVariables["GST_PLUGIN_PATH_1_0"]=plugins;si.EnvironmentVariables["GST_PLUGIN_SYSTEM_PATH_1_0"]=plugins;
                si.EnvironmentVariables["GST_PLUGIN_SCANNER_1_0"]=scanner;
                si.EnvironmentVariables["GST_REGISTRY_1_0"]=Path.Combine(Settings.DataFolder,"gstreamer-registry.bin");
                si.EnvironmentVariables["UXPLAYRC"]=Path.Combine(Settings.DataFolder,"empty.uxplayrc");
                si.EnvironmentVariables["HOME"]=Settings.DataFolder;
                if(!File.Exists(si.EnvironmentVariables["UXPLAYRC"]))File.WriteAllText(si.EnvironmentVariables["UXPLAYRC"],"");
            }
            Directory.CreateDirectory(Settings.DataFolder);
            var androidHome=Path.Combine(Settings.DataFolder,"android");
            Directory.CreateDirectory(androidHome);si.EnvironmentVariables["ANDROID_USER_HOME"]=androidHome;
            return si;
        }
        void OnProcessLine(object sender,DataReceivedEventArgs e) {
            var line=e.Data;
            if(string.IsNullOrEmpty(line))return;
            if(line.Length>MaximumLineLength)line=line.Substring(0,MaximumLineLength)+"…";
            lock(gate){if(disposed||!ReferenceEquals(sender,owned))return;lines.Enqueue(line);while(lines.Count>MaximumLogLines)lines.Dequeue();LastLine=line;}
            lock(callbackGate){if(disposed)return;var handlers=Log;if(handlers!=null)foreach(Action<string> handler in handlers.GetInvocationList())try{handler(line);}catch{}}
            Notify();
        }
        void OnExited(object sender,EventArgs e) {
            var p=(Process)sender;var value=ReadExitCode(p);SafeFileHandle finishedJob=null;bool notify;
            lock(gate){if(disposed||!ReferenceEquals(p,owned))return;exitCode=value;Video=IntPtr.Zero;notify=!Stopping;State=Stopping?Loc.T("Arrestato","Stopped"):Loc.T("Terminato","Exited")+(value.HasValue?" ("+value.Value+")":"");if(!Stopping){finishedJob=job;job=null;}}
            if(finishedJob!=null)finishedJob.Dispose();if(notify)Notify();
        }
        void Notify(){
            if(disposed)return;
            lock(callbackGate){if(disposed)return;var handlers=Changed;if(handlers!=null)foreach(Action handler in handlers.GetInvocationList())try{handler();}catch{}}
        }
        public void Poll() {
            Process p;IntPtr video;int id;
            lock(gate){if(disposed||Stopping||Demo||!IsAlive(owned))return;p=owned;video=Video;try{id=p.Id;}catch(InvalidOperationException){return;}}
            if(video!=IntPtr.Zero&&!Native.IsWindow(video))video=IntPtr.Zero;
            if(video==IntPtr.Zero)video=Native.FindVideoWindow(id);
            lock(gate){if(disposed||Stopping||!ReferenceEquals(p,owned)||!IsAlive(p))return;Video=video;State=video!=IntPtr.Zero?Loc.T("Schermo connesso","Screen connected"):Loc.T("In attesa del dispositivo","Waiting for the device");}
        }
        public Task StopAsync() {
            lock(gate){
                if(stopTask!=null&&!stopTask.IsCompleted)return stopTask;
                Stopping=true;Demo=false;var p=owned;var ownedJob=job;job=null;
                stopTask=Task.Run(()=>{
                    try{if(IsAlive(p)){try{p.CloseMainWindow();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}if(!p.WaitForExit(1000)){try{p.Kill();}catch(InvalidOperationException){}p.WaitForExit(3000);}}}
                    catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
                    finally{if(ownedJob!=null)ownedJob.Dispose();lock(gate){if(ReferenceEquals(owned,p)){var code=ReadExitCode(p);if(code.HasValue)exitCode=code;Video=IntPtr.Zero;State=Loc.T("Arrestato","Stopped");}}}
                });
                stopTask.ContinueWith(t=>Notify(),TaskScheduler.Default);
                return stopTask;
            }
        }
        public void Dispose(){
            lock(gate){if(disposed)return;disposed=true;}
            try{StopAsync().GetAwaiter().GetResult();}catch{}
            lock(gate){ReleaseProcess();}
            lock(callbackGate){Changed=null;Log=null;}
        }
        void ReleaseProcess(){
            var p=owned;owned=null;
            if(job!=null){job.Dispose();job=null;}
            if(p!=null){Detach(p);p.Dispose();}
        }
        void Detach(Process p){p.OutputDataReceived-=OnProcessLine;p.ErrorDataReceived-=OnProcessLine;p.Exited-=OnExited;try{p.EnableRaisingEvents=false;}catch(InvalidOperationException){}}
        static bool IsAlive(Process p){try{return p!=null&&!p.HasExited;}catch(InvalidOperationException){return false;}}
        static int? ReadExitCode(Process p){try{return p!=null&&p.HasExited?(int?)p.ExitCode:null;}catch(InvalidOperationException){return null;}}

        // Each job contains only the process started by this Session and its children.
        // Failure to assign (for example, host job restrictions) leaves bounded direct-process cleanup.
        internal static SafeFileHandle CreateOwnedJob(Process p){
            var handle=CreateJobObject(IntPtr.Zero,null);if(handle.IsInvalid){handle.Dispose();return null;}
            var limits=new JobLimits();limits.Basic.LimitFlags=0x2000;
            if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf(typeof(JobLimits)))||!AssignProcessToJobObject(handle,p.Handle)){handle.Dispose();return null;}
            return handle;
        }
        [StructLayout(LayoutKind.Sequential)]struct BasicJobLimits{public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
        [StructLayout(LayoutKind.Sequential)]struct JobIo{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
        [StructLayout(LayoutKind.Sequential)]struct JobLimits{public BasicJobLimits Basic;public JobIo Io;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateJobObject(IntPtr attributes,string name);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(SafeFileHandle job,int kind,ref JobLimits information,uint length);
        [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
    }
    public static class AdbDevices {
        public static async Task<string> Run(string args) {
            if(!File.Exists(Paths.Adb))throw new FileNotFoundException(Loc.T("Il componente Android non è presente. Ripristina i file dell'applicazione.","The Android component is missing. Restore the application files."));
            return await Task.Run(()=>{
                var si=new ProcessStartInfo(Paths.Adb,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(Paths.Adb)};
                var home=Path.Combine(Settings.DataFolder,"android");Directory.CreateDirectory(home);si.EnvironmentVariables["ANDROID_USER_HOME"]=home;
                using(var p=new Process{StartInfo=si}){
                    p.Start();var a=p.StandardOutput.ReadToEndAsync();var b=p.StandardError.ReadToEndAsync();
                    if(!p.WaitForExit(15000)){p.Kill();throw new TimeoutException(Loc.T("ADB non risponde.","ADB is not responding."));}
                    Task.WaitAll(a,b);return a.Result+Environment.NewLine+b.Result;
                }
            });
        }
        public static List<string[]> Parse(string text) {
            return text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries)).Where(a=>a.Length>=2&&new[]{"device","unauthorized","offline"}.Contains(a[1])).ToList();
        }
    }
}
