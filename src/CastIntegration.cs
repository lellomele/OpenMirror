using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OpenMirror {
    internal static class CastIntegration {
        public const int Port=18010;
        public static string BackendPath{get{return Path.Combine(Paths.Root,"bin","cast","OpenMirror.CastReceiver.exe");}}
        public static bool Available{get{return File.Exists(BackendPath)&&File.Exists(Path.Combine(Paths.Root,"bin","airplay","libgstreamer-1.0-0.dll"));}}
        public static string DataFolder{get{return Path.Combine(Settings.DataFolder,"chrome-cast");}}
        public static string CertificatePath{get{return Path.Combine(DataFolder,"dev-root.pem");}}
        public static async Task PrepareAsync(){
            if(!Available)throw new FileNotFoundException(Loc.T("Il ricevitore Chrome sperimentale non è presente. Ripristina i file dell'applicazione.","The experimental Chrome receiver is missing. Restore the application files."));
            Directory.CreateDirectory(DataFolder);
            await Task.Run(()=>{using(var p=new Process{StartInfo=new ProcessStartInfo(BackendPath,"--prepare --data "+Session.Quote(DataFolder)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(BackendPath)}}){p.Start();var output=p.StandardOutput.ReadToEndAsync();var errors=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(45000)){try{p.Kill();}catch{}throw new TimeoutException(Loc.T("Preparazione del certificato scaduta.","Certificate preparation timed out."));}Task.WaitAll(output,errors);if(p.ExitCode!=0)throw new InvalidOperationException(Loc.T("Impossibile preparare il certificato di sviluppo Chrome.","Could not prepare the Chrome developer certificate.")+Environment.NewLine+errors.Result);}}).ConfigureAwait(false);
        }
        public static string Arguments(Settings settings){return "--data "+Session.Quote(DataFolder)+" --runtime "+Session.Quote(Path.Combine(Paths.Root,"bin","airplay"))+" --name "+Session.Quote(settings.ReceiverName+(settings.Language=="en"?" · Chrome experimental":" · Chrome sperimentale"))+" --port "+Port+(settings.Audio?"":" --no-audio")+(settings.StretchVideo?" --stretch":"");}
        public static Process OpenDedicatedChrome(){
            if(!File.Exists(CertificatePath))throw new InvalidOperationException(Loc.T("Avvia prima il ricevitore Chrome sperimentale.","Start the experimental Chrome receiver first."));
            string chrome=null;foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}){string candidate=Path.Combine(root,"Google","Chrome","Application","chrome.exe");if(File.Exists(candidate)){chrome=candidate;break;}}
            if(chrome==null)throw new FileNotFoundException(Loc.T("Google Chrome non è installato nei percorsi previsti.","Google Chrome was not found in its usual installation locations."));
            string profile=Path.Combine(Settings.DataFolder,"chrome-cast-profile");Directory.CreateDirectory(profile);
            string args="--user-data-dir="+Session.Quote(profile)+" --cast-developer-certificate-path="+Session.Quote(CertificatePath)+" --no-first-run --no-default-browser-check --disable-sync --disable-extensions about:blank";
            return Process.Start(new ProcessStartInfo(chrome,args){UseShellExecute=false});
        }
    }
}
