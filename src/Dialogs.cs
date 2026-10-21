using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenMirror {
    internal sealed class SettingsDialog:Window {
        internal StackPanel FormBody{get;private set;}
        public SettingsDialog(Settings s){
            Ui.Style(this);Title=Loc.T("Impostazioni · OpenMirror","Settings · OpenMirror");Width=640;Height=740;MinWidth=460;MinHeight=400;Ui.FitWindow(this);ResizeMode=ResizeMode.CanResize;
            var dock=new DockPanel{Margin=new Thickness(24)};Content=dock;
            var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);dock.Children.Add(actions);
            var error=Ui.Label("",13,Ui.Danger);error.Margin=new Thickness(0,8,0,0);DockPanel.SetDock(error,Dock.Bottom);dock.Children.Add(error);
            var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};dock.Children.Add(scroll);
            var body=new StackPanel{Margin=new Thickness(0,0,12,0)};FormBody=body;scroll.Content=body;
            var heading=Ui.Label(Loc.T("Il ricevitore, come vuoi tu.","Your receiver, your way."),24);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(0,0,0,22);body.Children.Add(heading);
            var name=Ui.Edit(s.ReceiverName);Field(body,Loc.T("Nome del ricevitore AirPlay","AirPlay receiver name"),name);
            var lang=Combo(new object[]{"Italiano","English"},s.Language=="en"?1:0);Field(body,Loc.T("Lingua","Language"),lang);
            var resolutions=new[]{"1280x720","1920x1080","2560x1440","3840x2160"};var resolution=Combo(new object[]{"1280 × 720 · HD","1920 × 1080 · Full HD","2560 × 1440 · QHD","3840 × 2160 · 4K"},Array.IndexOf(resolutions,s.Resolution));Field(body,Loc.T("Risoluzione richiesta","Requested resolution"),resolution);
            var fps=Combo(new object[]{"15 fps","30 fps","60 fps"},Array.IndexOf(new[]{15,30,60},s.Fps));Field(body,Loc.T("Frequenza massima","Maximum frame rate"),fps);
            var fit=Combo(new object[]{Loc.T("Mantieni proporzioni","Keep aspect ratio"),Loc.T("Riempi l'area (può deformare l'immagine)","Fill the area (may stretch the image)")},s.StretchVideo?1:0);Field(body,Loc.T("Immagine","Picture"),fit);
            var audio=Check(Loc.T("Riproduci l'audio del dispositivo","Play device audio"),s.Audio);body.Children.Add(audio);
            var reconnect=Check(Loc.T("Riavvia automaticamente il ricevitore dopo un errore","Automatically restart the receiver after a failure"),s.AutoReconnect);body.Children.Add(reconnect);
            var software=Check(Loc.T("Decodifica software per problemi di compatibilità","Use software decoding for compatibility problems"),s.SoftwareDecoder);body.Children.Add(software);
            var pinOn=Check(Loc.T("Richiedi un PIN AirPlay","Require an AirPlay PIN"),s.RequirePin);body.Children.Add(pinOn);
            var pin=Ui.Edit(s.Pin);pin.MaxLength=4;pin.MinWidth=80;
            var pinRow=new DockPanel();var renew=Ui.Button(Loc.T("Nuovo PIN","New PIN"),()=>pin.Text=Settings.CreatePin());DockPanel.SetDock(renew,Dock.Right);pinRow.Children.Add(renew);pinRow.Children.Add(pin);Field(body,"PIN",pinRow);
            var folder=Ui.Edit(s.SaveFolder);Field(body,Loc.T("Cartella di video e screenshot","Recording and screenshot folder"),folder);
            var mic=Ui.Edit(s.Microphone);Field(body,Loc.T("Microfono DirectShow (facoltativo)","DirectShow microphone (optional)"),mic);
            var devices=Ui.Label("",12,Ui.Muted);var scan=Ui.Button(Loc.T("Rileva microfoni","Find microphones"),async()=>{try{devices.Text=string.Join(Environment.NewLine,await FindMicrophones());}catch(Exception ex){devices.Text=ex.Message;}});body.Children.Add(scan);body.Children.Add(devices);
            var note=Ui.Label(Loc.T("30 fps e Full HD privilegiano stabilità e latenza. Risoluzione e frequenza effettive dipendono dal dispositivo. Le modifiche alla ricezione si applicano alla riapertura del ricevitore. L'audio registrato è il microfono; l'audio del dispositivo non viene incluso nel file.","30 fps and Full HD favor stability and low latency. Actual resolution and frame rate depend on the device. Receiver changes apply after restarting it. Recorded audio comes from the microphone; device audio is not included in the file."),12,Ui.Muted);note.Margin=new Thickness(0,12,0,8);body.Children.Add(note);
            var cancel=Ui.Button(Loc.T("Annulla","Cancel"),()=>DialogResult=false);cancel.IsCancel=true;actions.Children.Add(cancel);
            var save=Ui.Button(Loc.T("Salva","Save"),()=>{
                if(string.IsNullOrWhiteSpace(name.Text)||name.Text.Trim().Length>60||name.Text.Any(char.IsControl)){error.Text=Loc.T("Usa un nome da 1 a 60 caratteri.","Use a name between 1 and 60 characters.");return;}
                if(pinOn.IsChecked==true&&(!Regex.IsMatch(pin.Text,"^[0-9]{4}$")||pin.Text=="0000")){error.Text=Loc.T("Usa un PIN di quattro cifre diverso da 0000.","Use a four-digit PIN other than 0000.");return;}
                if(!Path.IsPathRooted(folder.Text)){error.Text=Loc.T("Scegli un percorso completo per i file.","Choose an absolute folder path.");return;}
                try{Directory.CreateDirectory(folder.Text);}catch(Exception ex){error.Text=ex.Message;return;}
                s.ReceiverName=name.Text.Trim();s.Language=lang.SelectedIndex==1?"en":"it";s.Resolution=resolutions[resolution.SelectedIndex];s.Fps=new[]{15,30,60}[fps.SelectedIndex];s.StretchVideo=fit.SelectedIndex==1;s.Audio=audio.IsChecked==true;s.AutoReconnect=reconnect.IsChecked==true;s.SoftwareDecoder=software.IsChecked==true;s.RequirePin=pinOn.IsChecked==true;s.Pin=Regex.IsMatch(pin.Text,"^[0-9]{4}$")&&pin.Text!="0000"?pin.Text:Settings.CreatePin();s.SaveFolder=folder.Text;s.Microphone=mic.Text.Trim();DialogResult=true;
            },true);save.IsDefault=true;actions.Children.Add(save);
        }
        static ComboBox Combo(object[] items,int index){var c=new ComboBox{FontSize=14,Padding=new Thickness(10,7,10,7),MinHeight=36,Margin=new Thickness(0,7,0,18),Foreground=Brushes.Black};foreach(var i in items)c.Items.Add(i);c.SelectedIndex=Math.Max(0,index);return c;}
        static CheckBox Check(string text,bool value){return new CheckBox{Content=Ui.Label(text,14),IsChecked=value,Foreground=Ui.Text,Margin=new Thickness(0,6,0,12),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Center};}
        static void Field(StackPanel p,string label,UIElement content){var t=Ui.Label(label,13,Ui.Muted);p.Children.Add(t);p.Children.Add(content);}
        static Task<string[]> FindMicrophones(){return Task.Run(()=>{
            if(!File.Exists(Paths.Ffmpeg))throw new FileNotFoundException(Loc.T("FFmpeg non disponibile.","FFmpeg unavailable."));
            using(var p=new Process{StartInfo=new ProcessStartInfo(Paths.Ffmpeg,"-hide_banner -list_devices true -f dshow -i dummy"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}}){p.Start();var e=p.StandardError.ReadToEndAsync();var o=p.StandardOutput.ReadToEndAsync();if(!p.WaitForExit(10000)){p.Kill();throw new TimeoutException(Loc.T("Rilevamento scaduto.","Detection timed out."));}Task.WaitAll(e,o);return Regex.Matches(e.Result,"\"([^\"]+)\" \\(audio\\)").Cast<Match>().Select(m=>m.Groups[1].Value).Distinct().ToArray();}
        });}
    }
    internal sealed class AndroidDialog:Window {
        readonly ListBox devices=new ListBox{MinHeight=150,FontSize=14,Margin=new Thickness(0,12,0,8)};
        readonly TextBlock status=Ui.Label("",13,Ui.Muted);
        readonly TextBox endpoint=Ui.Edit(""),code=Ui.Edit("");
        readonly Button connect;
        bool loading;
        public string Serial;
        public AndroidDialog(){
            Ui.Style(this);Title=Loc.T("Collega Android","Connect Android");Width=620;Height=650;MinWidth=440;MinHeight=430;Ui.FitWindow(this);
            var dock=new DockPanel{Margin=new Thickness(24)};Content=dock;var bottom=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,15,0,0)};DockPanel.SetDock(bottom,Dock.Bottom);dock.Children.Add(bottom);
            var cancel=Ui.Button(Loc.T("Annulla","Cancel"),()=>DialogResult=false);cancel.IsCancel=true;bottom.Children.Add(cancel);connect=Ui.Button(Loc.T("Apri schermo","Open screen"),()=>{var row=devices.SelectedItem as DeviceRow;if(row==null||row.State!="device"){status.Text=Loc.T("Scegli un dispositivo autorizzato.","Choose an authorized device.");return;}Serial=row.Serial;DialogResult=true;},true);bottom.Children.Add(connect);
            var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dock.Children.Add(scroll);var p=new StackPanel();scroll.Content=p;
            p.Children.Add(Ui.Label(Loc.T("Android, in grande.","Android, on the big screen."),24));p.Children.Add(Ui.Label(Loc.T("Attiva Debug USB, collega il telefono e autorizza il PC sul dispositivo.","Enable USB debugging, connect the phone and authorize this PC on your device."),14,Ui.Muted));p.Children.Add(devices);p.Children.Add(status);p.Children.Add(Ui.Button(Loc.T("Aggiorna dispositivi","Refresh devices"),async()=>await Refresh()));
            var separator=Ui.Label(Loc.T("Wi-Fi ADB","Wi-Fi ADB"),18);separator.Margin=new Thickness(0,20,0,12);p.Children.Add(separator);p.Children.Add(Ui.Label(Loc.T("Indirizzo IP:porta mostrato in Debug wireless","IP:port shown in Wireless debugging"),13,Ui.Muted));p.Children.Add(endpoint);p.Children.Add(Ui.Button(Loc.T("Connetti Wi-Fi","Connect Wi-Fi"),async()=>await Network(false)));p.Children.Add(Ui.Label(Loc.T("Codice di associazione a sei cifre","Six-digit pairing code"),13,Ui.Muted));code.MaxLength=6;p.Children.Add(code);p.Children.Add(Ui.Button(Loc.T("Associa","Pair"),async()=>await Network(true)));p.Children.Add(Ui.Label(Loc.T("Associazione e connessione possono avere porte diverse. Dopo l'associazione usa la porta di connessione.","Pairing and connecting may use different ports. After pairing, use the connection port."),12,Ui.Muted));
            Loaded+=async(o,e)=>await Refresh();
        }
        async Task Refresh(){if(loading)return;loading=true;connect.IsEnabled=false;try{var text=await AdbDevices.Run("devices -l");devices.Items.Clear();foreach(var r in AdbDevices.Parse(text))devices.Items.Add(new DeviceRow{Serial=r[0],State=r[1]});if(devices.Items.Count>0)devices.SelectedIndex=0;status.Text=devices.Items.Count==0?Loc.T("Nessun dispositivo. Verifica cavo, driver e autorizzazione.","No device found. Check cable, drivers and authorization."):Loc.T("device = pronto · unauthorized = autorizza sul telefono · offline = ricollega","device = ready · unauthorized = authorize on phone · offline = reconnect");}catch(Exception ex){status.Text=ex.Message;}finally{loading=false;connect.IsEnabled=true;}}
        async Task Network(bool pair){if(loading)return;var ep=endpoint.Text.Trim();if(!Regex.IsMatch(ep,@"^[A-Za-z0-9.\-]+:[0-9]{1,5}$")){status.Text=Loc.T("Inserisci un indirizzo IP:porta valido.","Enter a valid IP:port address.");return;}int port;if(!int.TryParse(ep.Substring(ep.LastIndexOf(':')+1),out port)||port<1||port>65535){status.Text=Loc.T("Porta non valida.","Invalid port.");return;}if(pair&&!Regex.IsMatch(code.Text,"^[0-9]{6}$")){status.Text=Loc.T("Inserisci le sei cifre del codice.","Enter the six-digit code.");return;}loading=true;try{status.Text=await AdbDevices.Run((pair?"pair ":"connect ")+Session.Quote(ep)+(pair?" "+code.Text:""));}catch(Exception ex){status.Text=ex.Message;}finally{loading=false;}if(!pair)await Refresh();}
        sealed class DeviceRow{public string Serial,State;public override string ToString(){return Serial+" · "+State;}}
    }
    internal sealed class AboutDialog:Window {
        internal FrameworkElement Body{get;private set;}
        public AboutDialog(){
            Ui.Style(this);Title=Loc.T("Informazioni · OpenMirror","About · OpenMirror");Width=650;Height=660;MinWidth=440;MinHeight=400;Ui.FitWindow(this);
            var panel=new StackPanel{Margin=new Thickness(25)};Body=panel;Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            var title=Ui.Label(Edition.AirPlayOnly?"OpenMirror 0.2 · AirPlay":"OpenMirror 0.2",28);title.FontWeight=FontWeights.SemiBold;panel.Children.Add(title);var copy=Ui.Label(Loc.Copyright,14,Ui.Accent);copy.Margin=new Thickness(0,14,0,10);panel.Children.Add(copy);
            panel.Children.Add(Ui.Button("github.com/lellomele",()=>Paths.Open("https://github.com/lellomele")));
            var description=Ui.Label(Loc.T("Ricevitore open source per un solo dispositivo, con screenshot, registrazione e visualizzazione a tutto schermo.","Open source single-device receiver with screenshots, recording and fullscreen presentation."),14);description.Margin=new Thickness(0,15,0,20);panel.Children.Add(description);
            panel.Children.Add(Ui.Label(Loc.T("Compatibilità","Compatibility"),19));
            panel.Children.Add(Ui.Label(Edition.AirPlayOnly?Loc.T("AirPlay · UxPlay per iPad, iPhone e Mac.\nPC e dispositivo devono essere sulla stessa rete locale.\nQuesta edizione include soltanto la ricezione AirPlay.","AirPlay · UxPlay for iPad, iPhone and Mac.\nThe PC and device must be on the same local network.\nThis edition includes AirPlay reception only."):Loc.T("AirPlay · UxPlay.\nAndroid · scrcpy, con Debug USB o ADB Wi-Fi autorizzato.\nMiracast · ricevitore Windows dedicato; richiede hardware compatibile e il componente installato.\nGoogle Cast · modalità sperimentale per Chrome con certificato di sviluppo. Google Home standard su Android non supportato.","AirPlay · UxPlay.\nAndroid · scrcpy, using authorized USB debugging or Wi-Fi ADB.\nMiracast · dedicated Windows receiver; requires compatible hardware and the installed component.\nGoogle Cast · experimental Chrome mode with a developer certificate. Stock Android Google Home is unsupported."),14,Ui.Muted));
            var license=Ui.Label(Loc.T("Codice e icona OpenMirror: MIT. UxPlay: GPL-3.0-or-later. scrcpy: Apache-2.0. Le altre dipendenze mantengono le proprie licenze. Progetto indipendente da Reflector e dagli altri prodotti citati.","OpenMirror code and icon: MIT. UxPlay: GPL-3.0-or-later. scrcpy: Apache-2.0. Other dependencies retain their own licenses. Independent of Reflector and the other products mentioned."),12,Ui.Muted);license.Margin=new Thickness(0,20,0,18);panel.Children.Add(license);var close=Ui.Button(Loc.T("Chiudi","Close"),()=>Close());close.IsCancel=true;panel.Children.Add(close);
        }
    }
    internal sealed class LogDialog:Window {
        public LogDialog(string text){Ui.Style(this);Title=Loc.T("Diagnostica · OpenMirror","Diagnostics · OpenMirror");Width=820;Height=560;Ui.FitWindow(this);var p=new DockPanel{Margin=new Thickness(20)};Content=p;var close=Ui.Button(Loc.T("Chiudi","Close"),()=>Close());close.IsCancel=true;DockPanel.SetDock(close,Dock.Bottom);p.Children.Add(close);p.Children.Add(new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas"),FontSize=12});}
    }
}
