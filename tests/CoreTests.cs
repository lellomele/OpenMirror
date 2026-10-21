using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using OpenMirror;

// Console-only lifecycle helpers. No receiver, ADB, native video window or desktop capture is started.
internal static class CoreTests {
    [DllImport("kernel32.dll")]static extern uint SetErrorMode(uint mode);
    static int changes;
    static string Exe {get{return System.Reflection.Assembly.GetExecutingAssembly().Location;}}
    static void Assert(bool value,string text){if(!value)throw new Exception(text);}
    static Session New(){return new Session{Protocol="Test"};}
    static bool Gone(int id){try{using(var p=Process.GetProcessById(id))return p.HasExited;}catch(ArgumentException){return true;}}
    static int Child(string mode) {
        if(mode=="quick"){Console.Error.WriteLine("Intentional helper exit");return 42;}
        if(mode=="logs"){for(int i=0;i<300;i++)Console.WriteLine(i+" "+new string('x',5000));return 43;}
        if(mode=="tree") {
            var si=new ProcessStartInfo(Exe,"--child wait"){UseShellExecute=false,CreateNoWindow=true};
            using(var child=Process.Start(si))Console.WriteLine("child="+child.Id);
        }
        Thread.Sleep(30000);return 0;
    }
    public static int Main(string[] args) {
        SetErrorMode(1|2|0x8000);
        try {
            if(args.Length==2&&args[0]=="--child")return Child(args[1]);
            using(var demo=new Session()) {
                demo.StartVerifiedAsync("missing-backend.exe","").GetAwaiter().GetResult();
                Assert(demo.Process==null&&demo.Alive&&demo.Connected,"Default Demo must be process-free.");
                Assert(demo.StartedAt!=DateTime.MinValue&&!demo.ExitCode.HasValue,"Demo metadata invalid.");
                demo.StopAsync().GetAwaiter().GetResult();Assert(!demo.Alive,"Stopped demo must not remain alive.");
            }
            using(var absent=New()) {
                bool rejected=false;try{absent.Start("nonexistent-receiver.exe","");}catch(FileNotFoundException e){rejected=e.Message.Contains("receiver component");}
                Assert(rejected&&absent.Process==null,"Missing backend must fail in managed code.");
            }
            using(var preflight=new Session{Protocol="AirPlay"}) {
                bool rejected=false;try{preflight.Start(Exe,"--child wait");}catch(FileNotFoundException e){rejected=e.Message.Contains("libgstlevel.dll");}
                Assert(rejected&&preflight.Process==null,"Missing AirPlay plugins must fail before process creation.");
            }
            using(var quick=New()) {
                bool rejected=false;try{quick.StartVerifiedAsync(Exe,"--child quick").GetAwaiter().GetResult();}catch(InvalidOperationException e){rejected=e.Message.Contains("42");}
                Assert(rejected&&quick.ExitCode==42&&!quick.Alive,"Verified startup must report early process exit.");
            }
            using(var logs=New()) {
                logs.Log+=delegate(string ignored){throw new Exception("Subscriber failure must be contained");};
                logs.Changed+=delegate{throw new Exception("Subscriber failure must be contained");};
                try{logs.StartVerifiedAsync(Exe,"--child logs").GetAwaiter().GetResult();}catch(InvalidOperationException){}
                Assert(logs.ExitCode==43,"Log callback exception must not terminate lifecycle observation.");
                var text=logs.Diagnostic;
                Assert(text.Length<250000&&text.Split(new[]{Environment.NewLine},StringSplitOptions.None).Length<=120,"Diagnostic log must be bounded by lines and line length.");
            }
            int treeChild=0;bool contained;
            using(var live=New()) {
                live.Changed+=delegate{Interlocked.Increment(ref changes);};
                live.StartVerifiedAsync(Exe,"--child tree").GetAwaiter().GetResult();
                contained=live.OwnsProcessTree;
                foreach(var line in live.Diagnostic.Split(new[]{Environment.NewLine},StringSplitOptions.RemoveEmptyEntries))if(line.StartsWith("child="))treeChild=int.Parse(line.Substring(6));
                Assert(treeChild!=0&&live.Alive&&live.StartedAt!=DateTime.MinValue,"Long-lived helper startup metadata invalid.");
                bool duplicate=false;try{live.Start(Exe,"--child wait");}catch(InvalidOperationException){duplicate=true;}
                Assert(duplicate,"A running session must reject a second process.");
                var first=live.StopAsync();var second=live.StopAsync();Assert(object.ReferenceEquals(first,second),"Concurrent stops must share their completion task.");
                first.GetAwaiter().GetResult();Assert(!live.Alive&&live.ExitCode.HasValue,"Stop must retain the exit code.");
                live.Dispose();live.Poll();
                int snapshot=changes;Thread.Sleep(100);Assert(snapshot==changes,"Disposed session must not publish further callbacks.");
            }
            try {
                if(contained){for(int i=0;i<20&&!Gone(treeChild);i++)Thread.Sleep(50);Assert(Gone(treeChild),"Kill-on-close job must terminate the owned descendant.");}
            } finally {if(!Gone(treeChild))using(var p=Process.GetProcessById(treeChild))p.Kill();}
            using(var reentrant=New()) {
                reentrant.StartVerifiedAsync(Exe,"--child wait").GetAwaiter().GetResult();
                reentrant.Changed+=delegate{if(reentrant.Stopping)reentrant.Dispose();};
                reentrant.StopAsync().GetAwaiter().GetResult();
                for(int i=0;i<40&&reentrant.Process!=null;i++)Thread.Sleep(50);
                Assert(reentrant.Process==null,"Disposal from a completion callback must not deadlock its own stop task.");
            }
            var arguments=Session.AirplayArguments(new Settings{Resolution="2560x1440",StretchVideo=true,SoftwareDecoder=true},1);
            Assert(arguments.Contains("2560x1440")&&arguments.Contains("force-aspect-ratio=false")&&arguments.Contains(" -avdec"),"AirPlay resolution/stretch/software fallback must be retained.");
            Console.WriteLine("Core tests passed: Demo, preflight, early exit, bounded logs, event isolation, single-device ownership, idempotent stop/dispose; job containment="+contained+".");
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
