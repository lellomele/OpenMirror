using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Windows;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]
[assembly: AssemblyTitle("OpenMirror")]
[assembly: AssemblyDescription("Open source single-device mirroring receiver")]
[assembly: AssemblyCompany("prof. ing. Raffaele Mele")]
[assembly: AssemblyProduct("OpenMirror")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]
[assembly: AssemblyCopyright("(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele")]
namespace OpenMirror {
    internal static class AppLog {
        static readonly object Gate=new object();
        public static void Write(string message){try{lock(Gate){Directory.CreateDirectory(Settings.DataFolder);var p=Path.Combine(Settings.DataFolder,"OpenMirror.log");if(File.Exists(p)&&new FileInfo(p).Length>2*1024*1024){var previous=p+".previous";if(File.Exists(previous))File.Delete(previous);File.Move(p,previous);}if(message!=null&&message.Length>32768)message=message.Substring(0,32768);File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine);}}catch{}}
    }
    internal static class Loc {
        public static string Language="it";
        public static string T(string italian,string english){return Language=="en"?english:italian;}
        public const string Copyright="(C) 2026 prof. ing. Raffaele Mele - https://github.com/lellomele";
    }
    static class Program {
        [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
        [STAThread] static int Main(string[] args) {
            SetErrorMode(0x0001|0x0002|0x8000);
            try{
                try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}
                if(Array.IndexOf(args,"--verify-layout")>=0)return LayoutVerification.Run(args);
                bool first;
                using(var single=new Mutex(true,"Local\\OpenMirror.SingleReceiver",out first)){
                if(!first){ActivateExisting();return 0;}
                var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
                MainWindow main=null;
                app.DispatcherUnhandledException+=(o,e)=>{AppLog.Write(e.Exception.ToString());e.Handled=true;if(main!=null)main.ReportError(Loc.T("Operazione non riuscita. Puoi riprovare.","Operation failed. You can try again."),e.Exception.Message);};
                AppDomain.CurrentDomain.UnhandledException+=(o,e)=>AppLog.Write(Convert.ToString(e.ExceptionObject));
                main=new MainWindow(Array.IndexOf(args,"--demo")>=0);try{app.Run(main);return 0;}finally{single.ReleaseMutex();}}
            }catch(Exception ex){AppLog.Write(ex.ToString());return 1;}
        }
        static void ActivateExisting(){try{var self=Process.GetCurrentProcess();var path=Assembly.GetExecutingAssembly().Location;foreach(var p in Process.GetProcesses()){using(p){try{if(p.Id!=self.Id&&p.MainWindowHandle!=IntPtr.Zero&&string.Equals(p.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase)){ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);break;}}catch{}}}}catch{}}
    }
}
