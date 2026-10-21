using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OpenMirror {
    public sealed class MainWindow : Window {
        readonly Settings settings;
        readonly bool demo,verification;
        readonly Grid root=new Grid();
        readonly Grid stage=new Grid();
        readonly Border stageBorder;
        readonly FrameworkElement header,toolbar,footer;
        readonly Border notification;
        readonly TextBlock noticeText,status,receiverTitle,waitTitle,waitHelp,pinLabel,subtitle,elapsed;
        readonly Button airplay,android,miracast,cast,chromeButton,stop,shot,record,fullButton,settingsButton,aboutButton,filesButton,logButton;
        readonly ComboBox language=new ComboBox();
        readonly WindowsFormsHost host=new WindowsFormsHost();
        readonly VideoSurface surface=new VideoSurface();
        readonly StackPanel waiting;
        readonly Recorder recorder=new Recorder();
        readonly DispatcherTimer poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
        readonly DispatcherTimer demoTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        Session session,reportedFailure;
        uint miracastPid;
        IntPtr attached,failedHandle;
        bool busy,recordPending,closing,allowClose,fullscreen,recovering,changingFullscreen;
        int retries;DateTime started,recordStarted,nextRetry,savedUntil;string errorDetails="",savedMessage="";
        Rect savedBounds;WindowState savedState;double savedMinWidth,savedMinHeight;
        System.Drawing.Rectangle recordingBounds;
        ExitOverlay exitOverlay;
        internal Grid RootLayout{get{return root;}}
        internal bool HasVideo{get{return demo||attached!=IntPtr.Zero&&Native.IsWindow(attached);}}
        internal bool IsFullscreen{get{return fullscreen;}}
        public MainWindow(bool demoMode):this(demoMode,false){}
        internal MainWindow(bool demoMode,bool verify){
            demo=demoMode;verification=verify;settings=Settings.Load();Loc.Language=settings.Language;Ui.Style(this);
            Title="OpenMirror";Width=1140;Height=760;MinWidth=760;MinHeight=540;WindowStartupLocation=WindowStartupLocation.CenterScreen;
            if(!verify)Ui.FitWindow(this);
            var icon=Path.Combine(Paths.Root,"assets","openmirror-256.png");if(File.Exists(icon)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(icon);bitmap.EndInit();bitmap.Freeze();Icon=bitmap;}
            root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            root.Background=Ui.Background;Content=root;
            var heading=new Grid{Margin=new Thickness(22,16,14,8)};heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            subtitle=Ui.Label("",13,Ui.Muted);var brand=Ui.Label("OpenMirror",27);brand.FontWeight=FontWeights.SemiBold;var titleStack=Ui.Vertical(brand,subtitle);heading.Children.Add(titleStack);
            var menu=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,MaxWidth=540};Grid.SetColumn(menu,1);heading.Children.Add(menu);
            heading.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});heading.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            heading.SizeChanged+=(o,e)=>{bool compact=heading.ActualWidth<700;Grid.SetRow(menu,compact?1:0);Grid.SetColumn(menu,compact?0:1);Grid.SetColumnSpan(menu,compact?2:1);menu.HorizontalAlignment=compact?HorizontalAlignment.Left:HorizontalAlignment.Right;menu.MaxWidth=compact?Math.Max(200,heading.ActualWidth):540;menu.Margin=compact?new Thickness(0,12,0,0):new Thickness(0);};
            filesButton=Ui.Button("",OpenFiles);settingsButton=Ui.Button("",OpenSettings);aboutButton=Ui.Button("",OpenAbout);
            language.Items.Add("Italiano");language.Items.Add("English");language.SelectedIndex=settings.Language=="en"?1:0;language.MinHeight=38;language.MinWidth=106;language.Margin=new Thickness(0,0,8,8);language.FontSize=14;language.SelectionChanged+=(o,e)=>ChangeLanguage();
            menu.Children.Add(filesButton);menu.Children.Add(settingsButton);menu.Children.Add(aboutButton);menu.Children.Add(language);header=heading;root.Children.Add(header);
            var actions=new WrapPanel{Margin=new Thickness(22,0,14,0)};Grid.SetRow(actions,1);root.Children.Add(actions);toolbar=actions;
            airplay=Ui.Button("AirPlay",async()=>await StartAirplay(),true);android=Ui.Button("Android",async()=>await StartAndroid());miracast=Ui.Button("Miracast",async()=>await StartMiracast());cast=Ui.Button("Google Cast · beta",async()=>await StartCast());chromeButton=Ui.Button("",OpenChrome);
            stop=Ui.Button("",async()=>await StopReceiver());shot=Ui.Button("",Screenshot);record=Ui.Button("",async()=>await ToggleRecord());fullButton=Ui.Button("",async()=>await SetFullscreen(!fullscreen));
            record.Width=190;
            foreach(var b in Edition.AirPlayOnly?new[]{airplay,stop,shot,record,fullButton}:new[]{airplay,android,miracast,cast,chromeButton,stop,shot,record,fullButton})actions.Children.Add(b);
            noticeText=Ui.Label("",13,Ui.Danger);noticeText.Margin=new Thickness(0,0,12,0);var message=new DockPanel();var dismiss=Ui.Button("×",()=>notification.Visibility=Visibility.Collapsed);dismiss.MinWidth=28;dismiss.Margin=new Thickness(0);dismiss.Padding=new Thickness(8,2,8,2);DockPanel.SetDock(dismiss,Dock.Right);message.Children.Add(dismiss);message.Children.Add(noticeText);
            notification=Ui.Box(message,new Thickness(12,8,12,8));notification.Background=Ui.B("#352128");notification.Margin=new Thickness(22,2,22,8);notification.Visibility=Visibility.Collapsed;Grid.SetRow(notification,2);root.Children.Add(notification);
            stage.Background=Brushes.Black;stage.ClipToBounds=true;
            waitTitle=Ui.Label("",28);waitTitle.TextAlignment=TextAlignment.Center;waitTitle.FontWeight=FontWeights.SemiBold;
            receiverTitle=Ui.Label("",18,Ui.Accent);receiverTitle.TextAlignment=TextAlignment.Center;receiverTitle.Margin=new Thickness(0,14,0,0);
            pinLabel=Ui.Label("",22,Ui.Accent);pinLabel.TextAlignment=TextAlignment.Center;pinLabel.Margin=new Thickness(0,12,0,8);
            waitHelp=Ui.Label("",14,Ui.Muted);waitHelp.TextAlignment=TextAlignment.Center;waitHelp.Margin=new Thickness(0,14,0,0);
            waiting=Ui.Vertical(waitTitle,receiverTitle,pinLabel,waitHelp);waiting.VerticalAlignment=VerticalAlignment.Center;waiting.HorizontalAlignment=HorizontalAlignment.Center;waiting.MaxWidth=650;waiting.Margin=new Thickness(30,22,30,22);stage.Children.Add(new ScrollViewer{Content=waiting,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalContentAlignment=VerticalAlignment.Center});
            if(!verification){host.Child=surface;host.Visibility=Visibility.Collapsed;stage.Children.Add(host);surface.Resize+=(o,e)=>ResizeVideo();surface.Demo=demo;}
            stageBorder=Ui.Box(stage,new Thickness(0));stageBorder.Margin=new Thickness(22,4,22,10);Grid.SetRow(stageBorder,3);root.Children.Add(stageBorder);
            var bottom=new Grid{Margin=new Thickness(22,0,14,10)};bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            status=Ui.Label("",13,Ui.Muted);status.Margin=new Thickness(0,4,12,4);bottom.Children.Add(status);elapsed=Ui.Label("",13,Ui.Accent);elapsed.Margin=new Thickness(8,0,14,0);Grid.SetColumn(elapsed,1);bottom.Children.Add(elapsed);logButton=Ui.Button("Log",OpenLog);logButton.MinHeight=30;logButton.Padding=new Thickness(12,4,12,4);logButton.Margin=new Thickness(0);Grid.SetColumn(logButton,2);bottom.Children.Add(logButton);footer=bottom;Grid.SetRow(footer,4);root.Children.Add(footer);
            recorder.Error+=text=>Dispatch(()=>{recordPending=false;UpdateUi();ReportError(Loc.T("Registrazione interrotta.","Recording stopped."),text);});
            recorder.Completed+=path=>Dispatch(()=>{recordPending=false;ShowSaved(path);UpdateUi();});
            poll.Tick+=async(o,e)=>await Poll();demoTimer.Tick+=(o,e)=>surface.Invalidate();
            Loaded+=(o,e)=>{if(verification)return;poll.Start();if(demo){host.Visibility=Visibility.Visible;waiting.Visibility=Visibility.Collapsed;demoTimer.Start();}try{settings.Save();}catch(Exception ex){AppLog.Write(ex.ToString());}};
            PreviewKeyDown+=async(o,e)=>{if(e.Key==Key.F11||e.Key==Key.Escape&&fullscreen){e.Handled=true;await SetFullscreen(!fullscreen);}else if((Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))==(ModifierKeys.Control|ModifierKeys.Shift)){if(e.Key==Key.S){e.Handled=true;Screenshot();}else if(e.Key==Key.R){e.Handled=true;await ToggleRecord();}}};
            Closing+=async(o,e)=>{if(allowClose)return;e.Cancel=true;if(closing)return;closing=true;IsEnabled=false;poll.Stop();demoTimer.Stop();
                try{if(exitOverlay!=null)exitOverlay.Close();while(busy||recordPending||changingFullscreen)await Task.Delay(50);
                    try{await recorder.StopAsync();}catch(Exception ex){AppLog.Write(ex.ToString());}
                    try{if(miracastPid!=0)await MiracastIntegration.StopAsync(miracastPid);}catch(Exception ex){AppLog.Write(ex.ToString());}
                    try{await ClearSession();}catch(Exception ex){AppLog.Write(ex.ToString());}
                }finally{UnregisterKeys();recorder.Dispose();surface.Dispose();allowClose=true;Close();}};
            SourceInitialized+=(o,e)=>{var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source.AddHook(WindowHook);};
            Activated+=(o,e)=>{if(fullscreen){RegisterKeys();if(exitOverlay!=null)exitOverlay.Topmost=true;}};Deactivated+=(o,e)=>{if(fullscreen){UnregisterKeys();Dispatcher.BeginInvoke(new Action(()=>{if(exitOverlay==null)return;var foreground=GetForegroundWindow();exitOverlay.Topmost=foreground==new WindowInteropHelper(this).Handle||foreground==new WindowInteropHelper(exitOverlay).Handle;}));}};
            ApplyLanguage();UpdateUi();
        }
        void Dispatch(Action action){if(closing)return;try{Dispatcher.BeginInvoke(action);}catch(InvalidOperationException){}}
        void ChangeLanguage(){if(language.SelectedIndex<0)return;settings.Language=language.SelectedIndex==1?"en":"it";Loc.Language=settings.Language;try{if(!verification)settings.Save();}catch(Exception ex){AppLog.Write(ex.ToString());}ApplyLanguage();UpdateUi();}
        void ApplyLanguage(){
            subtitle.Text=Loc.T("Un dispositivo. Tutto lo schermo.","One device. The whole screen.");filesButton.Content=Loc.T("Registrazioni","Recordings");settingsButton.Content=Loc.T("Impostazioni","Settings");aboutButton.Content=Loc.T("Informazioni","About");stop.Content=Loc.T("Disconnetti","Disconnect");shot.Content="Screenshot";fullButton.Content=Loc.T("Schermo intero","Fullscreen");record.Content=Loc.T("Registra","Record");
            cast.ToolTip=Loc.T("Sperimentale: Chrome con certificato di sviluppo e profilo dedicato. Google Home su Android non supportato.","Experimental: Chrome with a developer certificate and dedicated profile. Android Google Home is unsupported.");chromeButton.Content=Loc.T("Apri Chrome di prova","Open test Chrome");
            if(exitOverlay!=null)exitOverlay.RefreshLanguage();
        }
        void UpdateUi(){
            bool active=session!=null;bool external=miracastPid!=0&&MiracastIntegration.IsRunning(miracastPid);bool locked=busy||closing||recordPending||recorder.IsRecording;
            airplay.IsEnabled=android.IsEnabled=miracast.IsEnabled=!active&&!external&&!locked;cast.IsEnabled=!active&&!external&&!locked&&CastIntegration.Available;chromeButton.Visibility=active&&session.Protocol=="ChromeCast"?Visibility.Visible:Visibility.Collapsed;chromeButton.IsEnabled=!locked&&active&&session.Alive;stop.IsEnabled=(active||external)&&!busy&&!recordPending&&!recorder.IsRecording;
            shot.IsEnabled=HasVideo&&!busy&&!recordPending;record.IsEnabled=(HasVideo&&File.Exists(Paths.Ffmpeg)||recorder.IsRecording)&&!busy&&!recordPending;
            fullButton.IsEnabled=!external&&!busy&&!recordPending;settingsButton.IsEnabled=filesButton.IsEnabled=aboutButton.IsEnabled=language.IsEnabled=!locked;logButton.IsEnabled=!recorder.IsRecording;
            if(!recorder.IsRecording)record.Content=Loc.T("Registra","Record");else record.Content=Loc.T("Ferma registrazione","Stop recording");
            receiverTitle.Text=active?session.Name:"";pinLabel.Text=active&&session.Protocol=="AirPlay"&&settings.RequirePin?"PIN  "+settings.Pin:"";
            receiverTitle.Visibility=active?Visibility.Visible:Visibility.Collapsed;pinLabel.Visibility=pinLabel.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
            if(!active){waitTitle.Text=Loc.T("Il tuo schermo, qui.","Your screen, right here.");waitHelp.Text=Edition.AirPlayOnly?Loc.T("Scegli AirPlay, poi apri Duplica schermo su iPad, iPhone o Mac.\nUn solo dispositivo può usare il ricevitore alla volta.","Choose AirPlay, then open Screen Mirroring on your iPad, iPhone or Mac.\nOnly one device can use the receiver at a time."):Loc.T("Scegli AirPlay per iPad, iPhone o Mac.\nPer Android, collega il dispositivo tramite USB o Wi-Fi ADB.\nUn solo dispositivo può usare il ricevitore alla volta.","Choose AirPlay for iPad, iPhone or Mac.\nFor Android, connect through USB or Wi-Fi ADB.\nOnly one device can use the receiver at a time.");status.Text=demo?Loc.T("DEMO · nessun dispositivo reale","DEMO · no real device"):Loc.T("Pronto · nessun ricevitore attivo","Ready · no active receiver");}
            else{waitTitle.Text=Loc.T("In attesa del dispositivo","Waiting for your device");waitHelp.Text=session.Protocol=="AirPlay"?Loc.T("Sul dispositivo apri Duplica schermo e scegli il nome qui sopra.\nIl PC e il dispositivo devono essere sulla stessa rete locale.","On your device, open Screen Mirroring and choose the name above.\nThe PC and your device must be on the same local network."):Loc.T("Autorizza il collegamento sul dispositivo.\nLa ricezione comparirà qui automaticamente.","Authorize the connection on your device.\nThe mirrored display will appear here automatically.");status.Text=session.Protocol+" · "+(HasVideo?Loc.T("connesso","connected"):recovering?Loc.T("ripristino in corso…","recovering…"):session.Alive?Loc.T("in attesa","waiting"):Loc.T("ricevitore arrestato","receiver stopped"));}
            if(external){waitTitle.Text=Loc.T("Miracast nella finestra dedicata","Miracast in its dedicated window");waitHelp.Text=Loc.T("Sul MateBook premi Win+K e scegli il PC ricevente.\nAccetta la connessione nella finestra OpenMirror Miracast.\nDisconnetti questa sessione prima di usare AirPlay o Android ADB.","On the sending PC, press Win+K and choose the receiver.\nAccept the connection in the OpenMirror Miracast window.\nDisconnect this session before using AirPlay or Android ADB.");status.Text=Loc.T("Miracast · componente aperto","Miracast · component open");}
            if(active&&session.Protocol=="ChromeCast"){waitHelp.Text=Loc.T("Modalità sperimentale per Chrome. Premi Apri Chrome di prova, poi usa Trasmetti per condividere una scheda.\nPer un altro PC sorgente trasferisci soltanto il certificato pubblico e segui le istruzioni in bin/cast.\nGoogle Home su Android non è supportato.","Experimental Chrome mode. Click Open test Chrome, then use Cast to share a tab.\nFor another source PC, transfer only the public certificate and follow the instructions in bin/cast.\nAndroid Google Home is unsupported.");status.Text=Loc.T("Chrome Cast sperimentale","Experimental Chrome Cast")+" · "+(HasVideo?Loc.T("connesso","connected"):session.Alive?Loc.T("in attesa","waiting"):Loc.T("arrestato","stopped"));}
            if(DateTime.UtcNow<savedUntil)status.Text=savedMessage;else status.ToolTip=null;
            if(verification)return;
            bool show=HasVideo;host.Visibility=show?Visibility.Visible:Visibility.Collapsed;waiting.Visibility=show?Visibility.Collapsed:Visibility.Visible;
        }
        public void ReportError(string message,string details){if(closing)return;errorDetails=details??"";noticeText.Text=message;noticeText.ToolTip=details;notification.Visibility=Visibility.Visible;AppLog.Write(message+" "+details);if(fullscreen&&exitOverlay!=null)exitOverlay.ShowError(message);}
        async Task StartAirplay(){
            if(busy||session!=null||miracastPid!=0&&MiracastIntegration.IsRunning(miracastPid)||closing)return;busy=true;UpdateUi();
            Exception failure=null;
            try{
                var used=System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();if(used.Any(p=>p.Port>=7100&&p.Port<=7102))throw new InvalidOperationException(Loc.T("Le porte 7100–7102 sono già in uso. Chiudi l'altro ricevitore e riprova.","Ports 7100–7102 are already in use. Close the other receiver and try again."));
                notification.Visibility=Visibility.Collapsed;retries=0;await LaunchAirplay();
            }catch(Exception ex){failure=ex;}
            try{if(failure!=null){await ClearSession();ReportError(Loc.T("Impossibile avviare AirPlay.","Could not start AirPlay."),failure.Message);}}finally{busy=false;UpdateUi();}
        }
        async Task LaunchAirplay(){
            var s=new Session{Name=settings.ReceiverName,Protocol="AirPlay",Slot=1};session=s;started=DateTime.UtcNow;
            await s.StartVerifiedAsync(Paths.Airplay,Session.AirplayArguments(settings,1)+(retries>=2&&!settings.SoftwareDecoder?" -avdec":""));
        }
        async Task StartAndroid(){
            if(Edition.AirPlayOnly)return;
            if(busy||session!=null||miracastPid!=0&&MiracastIntegration.IsRunning(miracastPid)||closing)return;
            var dialog=new AndroidDialog();dialog.Owner=this;if(dialog.ShowDialog()!=true)return;
            busy=true;UpdateUi();Exception failure=null;try{
                var s=new Session{Name=dialog.Serial,Serial=dialog.Serial,Protocol="Android"};session=s;retries=0;started=DateTime.UtcNow;
                var size=settings.Resolution.Split('x')[0];var args="--serial "+Session.Quote(s.Serial)+" --window-title "+Session.Quote("OpenMirror - Android")+" --max-size="+size+" --max-fps="+settings.Fps+" --video-bit-rate=12M --video-buffer=0"+(settings.Audio?"":" --no-audio");
                await s.StartVerifiedAsync(Paths.Android,args);notification.Visibility=Visibility.Collapsed;
            }catch(Exception ex){failure=ex;}
            try{if(failure!=null){await ClearSession();ReportError(Loc.T("Impossibile avviare Android.","Could not start Android."),failure.Message);}}finally{busy=false;UpdateUi();}
        }
        async Task StartMiracast(){
            if(Edition.AirPlayOnly)return;
            if(busy||session!=null||miracastPid!=0&&MiracastIntegration.IsRunning(miracastPid)||closing)return;busy=true;UpdateUi();
            try{var size=settings.Resolution.Split('x');var result=await MiracastIntegration.StartAsync(this,settings.Language,settings.ReceiverName,int.Parse(size[0]),int.Parse(size[1]),settings.Fps,true);
                if(result.Success){miracastPid=result.ProcessId;notification.Visibility=Visibility.Collapsed;}else ReportError(result.Message,"");
            }catch(Exception ex){ReportError(Loc.T("Miracast non avviato.","Miracast did not start."),ex.Message);}finally{busy=false;UpdateUi();}
        }
        async Task StartCast(){
            if(Edition.AirPlayOnly)return;
            if(busy||session!=null||miracastPid!=0&&MiracastIntegration.IsRunning(miracastPid)||closing)return;busy=true;UpdateUi();
            Exception failure=null;try{try{var used=System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();if(used.Any(p=>p.Port==CastIntegration.Port))throw new InvalidOperationException(Loc.T("La porta del ricevitore Chrome è già in uso. Chiudi l'altro ricevitore e riprova.","The Chrome receiver port is already in use. Close the other receiver and try again.")+" ("+CastIntegration.Port+")");
                await CastIntegration.PrepareAsync();if(closing)return;var s=new Session{Name=settings.ReceiverName,Protocol="ChromeCast"};session=s;retries=0;started=DateTime.UtcNow;await s.StartVerifiedAsync(CastIntegration.BackendPath,CastIntegration.Arguments(settings));notification.Visibility=Visibility.Collapsed;
            }catch(Exception ex){failure=ex;}if(failure!=null){await ClearSession();ReportError(Loc.T("Chrome Cast sperimentale non avviato.","Experimental Chrome Cast did not start."),failure.Message);}}finally{busy=false;UpdateUi();}
        }
        void OpenChrome(){try{using(var process=CastIntegration.OpenDedicatedChrome()){} }catch(Exception ex){ReportError(Loc.T("Impossibile aprire Chrome di prova.","Could not open test Chrome."),ex.Message);}}
        async Task ClearSession(){var s=session;session=null;attached=failedHandle=IntPtr.Zero;if(s!=null){try{await s.StopAsync();}finally{s.Dispose();}}}
        async Task StopReceiver(){if(busy||recordPending)return;busy=true;UpdateUi();try{await recorder.StopAsync();if(miracastPid!=0){await MiracastIntegration.StopAsync(miracastPid);miracastPid=0;}await ClearSession();retries=0;recovering=false;}catch(Exception ex){ReportError(Loc.T("Arresto non completato.","Could not complete shutdown."),ex.Message);}finally{busy=false;UpdateUi();}}
        async Task Poll(){
            if(closing)return;
            if(miracastPid!=0&&!MiracastIntegration.IsRunning(miracastPid))miracastPid=0;
            if(session!=null&&!busy&&!recovering){
                session.Poll();
                if(session.Video!=IntPtr.Zero&&session.Video!=attached&&session.Video!=failedHandle){try{Native.Attach(session.Video,surface.Handle);attached=session.Video;ResizeVideo();}catch(Exception ex){failedHandle=session.Video;ReportError(Loc.T("Il video è aperto in una finestra separata.","Video opened in a separate window."),ex.Message);}}
                if(attached!=IntPtr.Zero&&!Native.IsWindow(attached)){attached=IntPtr.Zero;failedHandle=IntPtr.Zero;}
                if(!session.Alive&&!session.Stopping&&settings.AutoReconnect&&session.Protocol=="AirPlay"&&retries<2){recovering=true;nextRetry=DateTime.UtcNow.AddSeconds(2);}
                if(!session.Alive&&!session.Stopping&&!recovering&&!object.ReferenceEquals(session,reportedFailure)){reportedFailure=session;ReportError(Loc.T("Il ricevitore si è fermato. Premi Disconnetti e riprova.","The receiver stopped. Disconnect and try again."),session.Diagnostic);}
            }
            if(recovering&&!busy&&DateTime.UtcNow>=nextRetry){
                busy=true;try{string log=session==null?"":session.Diagnostic;retries++;await ClearSession();if(closing)return;await LaunchAirplay();notification.Visibility=Visibility.Collapsed;AppLog.Write("Receiver restarted, attempt "+retries+". "+log);}catch(Exception ex){ReportError(Loc.T("Il ricevitore si è fermato. Premi Disconnetti e riprova.","The receiver stopped. Disconnect and try again."),ex.Message);}finally{busy=false;recovering=false;}
            }
            if(recorder.IsRecording){elapsed.Text=(DateTime.Now-recordStarted).ToString(@"mm\:ss");if(!recordPending&&surface.RectangleToScreen(surface.ClientRectangle)!=recordingBounds){await StopRecording();ReportError(Loc.T("Registrazione fermata: area video modificata.","Recording stopped: video area changed."),"");}}else elapsed.Text="";
            UpdateUi();
        }
        void ResizeVideo(){if(attached!=IntPtr.Zero)Native.Resize(attached,surface.ClientSize.Width,surface.ClientSize.Height);}
        void ShowSaved(string path){savedMessage=Loc.T("Salvato: ","Saved: ")+Path.GetFileName(path);savedUntil=DateTime.UtcNow.AddSeconds(5);status.Text=savedMessage;status.ToolTip=path;}
        void Screenshot(){if(!HasVideo||verification)return;try{var path=Paths.Output(settings,".png");Native.Screenshot(surface,path);ShowSaved(path);}catch(Exception ex){ReportError(Loc.T("Screenshot non riuscito.","Screenshot failed."),ex.Message);}}
        async Task ToggleRecord(){if(busy||recordPending)return;if(recorder.IsRecording){await StopRecording();return;}if(!HasVideo)return;recordPending=true;UpdateUi();try{recordingBounds=surface.RectangleToScreen(surface.ClientRectangle);await recorder.StartAsync(surface,Paths.Ffmpeg,Paths.Output(settings,".mp4"),settings.Fps,settings.Microphone);recordStarted=DateTime.Now;}catch(Exception ex){ReportError(Loc.T("Registrazione non avviata.","Could not start recording."),ex.Message);}finally{recordPending=false;UpdateUi();}}
        async Task StopRecording(){if(recordPending)return;recordPending=true;try{await recorder.StopAsync();}catch(Exception ex){ReportError(Loc.T("Arresto registrazione non riuscito.","Could not stop recording."),ex.Message);}finally{recordPending=false;UpdateUi();}}
        internal async Task SetFullscreen(bool on){
            if(busy||closing||recordPending||changingFullscreen||fullscreen==on||on&&miracastPid!=0&&MiracastIntegration.IsRunning(miracastPid))return;changingFullscreen=true;
            try{if(recorder.IsRecording)await StopRecording();if(closing||fullscreen==on)return;
            if(on){savedBounds=new Rect(Left,Top,Width,Height);savedState=WindowState;savedMinWidth=MinWidth;savedMinHeight=MinHeight;MinWidth=0;MinHeight=0;WindowState=WindowState.Normal;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;MaxHeight=double.PositiveInfinity;
                header.Visibility=toolbar.Visibility=footer.Visibility=notification.Visibility=Visibility.Collapsed;stageBorder.Margin=new Thickness(0);stageBorder.BorderThickness=new Thickness(0);stageBorder.CornerRadius=new CornerRadius(0);Grid.SetRow(stageBorder,0);Grid.SetRowSpan(stageBorder,5);fullscreen=true;
                if(!verification){var handle=new WindowInteropHelper(this).Handle;var bounds=Forms.Screen.FromHandle(handle).Bounds;SetWindowPos(handle,IntPtr.Zero,bounds.Left,bounds.Top,bounds.Width,bounds.Height,0x0020|0x0014);exitOverlay=new ExitOverlay(async()=>await SetFullscreen(false)){Owner=this};exitOverlay.Show();PositionExit();RegisterKeys();}
            }else{
                fullscreen=false;UnregisterKeys();if(exitOverlay!=null){exitOverlay.Close();exitOverlay=null;}WindowStyle=WindowStyle.SingleBorderWindow;ResizeMode=ResizeMode.CanResize;header.Visibility=toolbar.Visibility=footer.Visibility=Visibility.Visible;stageBorder.Margin=new Thickness(22,4,22,10);stageBorder.BorderThickness=new Thickness(1);stageBorder.CornerRadius=new CornerRadius(10);Grid.SetRow(stageBorder,3);Grid.SetRowSpan(stageBorder,1);
                WindowState=WindowState.Normal;MinWidth=savedMinWidth;MinHeight=savedMinHeight;Left=savedBounds.Left;Top=savedBounds.Top;Width=savedBounds.Width;Height=savedBounds.Height;WindowState=savedState;
            }UpdateLayout();ResizeVideo();}finally{changingFullscreen=false;}
        }
        void PositionExit(){if(exitOverlay==null)return;var handle=new WindowInteropHelper(this).Handle;var screen=Forms.Screen.FromHandle(handle).Bounds;var source=PresentationSource.FromVisual(this);var scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;SetWindowPos(new WindowInteropHelper(exitOverlay).Handle,new IntPtr(-1),screen.Right-(int)(126*scale)-12,screen.Top+12,(int)(126*scale),(int)(38*scale),0x0010);}
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hgt,uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
        void RegisterKeys(){var h=new WindowInteropHelper(this).Handle;RegisterHotKey(h,201,0x4000,0x1B);RegisterHotKey(h,202,0x4000,0x7A);RegisterHotKey(h,203,0x4006,0x52);RegisterHotKey(h,204,0x4006,0x53);}
        void UnregisterKeys(){var h=new WindowInteropHelper(this).Handle;if(h!=IntPtr.Zero)foreach(var id in new[]{201,202,203,204})UnregisterHotKey(h,id);}
        IntPtr WindowHook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x0312&&fullscreen){var id=w.ToInt32();if(id==201||id==202){handled=true;Dispatcher.BeginInvoke(new Action(async()=>await SetFullscreen(false)));}else if(id==203){handled=true;Dispatcher.BeginInvoke(new Action(async()=>await ToggleRecord()));}else if(id==204){handled=true;Dispatcher.BeginInvoke(new Action(Screenshot));}}if(msg==0x02E0&&fullscreen)Dispatcher.BeginInvoke(new Action(()=>{PositionExit();ResizeVideo();}));return IntPtr.Zero;}
        void OpenSettings(){if(busy||recorder.IsRecording)return;var dialog=new SettingsDialog(settings){Owner=this};if(dialog.ShowDialog()==true){Loc.Language=settings.Language;language.SelectedIndex=settings.Language=="en"?1:0;ApplyLanguage();try{settings.Save();if(session!=null)ReportError(Loc.T("Impostazioni salvate. Disconnetti e riapri il ricevitore per applicarle.","Settings saved. Disconnect and restart the receiver to apply them."),"");}catch(Exception ex){ReportError(Loc.T("Salvataggio non riuscito.","Could not save settings."),ex.Message);}UpdateUi();}}
        void OpenAbout(){new AboutDialog{Owner=this}.ShowDialog();}
        void OpenFiles(){try{Directory.CreateDirectory(settings.SaveFolder);Paths.Open(settings.SaveFolder);}catch(Exception ex){ReportError(Loc.T("Impossibile aprire la cartella.","Could not open folder."),ex.Message);}}
        void OpenLog(){new LogDialog((session==null?"":session.Diagnostic)+Environment.NewLine+errorDetails){Owner=this}.ShowDialog();}
    }
    internal sealed class ExitOverlay:Window {
        readonly Button exit;
        string error="";
        public ExitOverlay(Action action){Ui.Style(this);WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Width=126;Height=38;Background=Brushes.Black;exit=Ui.Button("",action);exit.Margin=new Thickness(0);exit.MinWidth=0;exit.MinHeight=0;exit.Padding=new Thickness(9,5,9,5);exit.FontSize=12;Content=exit;RefreshLanguage();}
        public void RefreshLanguage(){exit.Content=error.Length==0?Loc.T("Esci · Esc","Exit · Esc"):Loc.T("Errore · Esci","Error · Exit");}
        public void ShowError(string message){error=message;exit.ToolTip=message;exit.Background=Ui.B("#662B39");RefreshLanguage();}
    }
    internal sealed class VideoSurface:Forms.Panel {
        public bool Demo;
        public VideoSurface(){BackColor=System.Drawing.Color.Black;DoubleBuffered=true;}
        protected override void OnPaint(Forms.PaintEventArgs e){base.OnPaint(e);if(!Demo)return;var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var fill=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,System.Drawing.Color.FromArgb(27,66,80),System.Drawing.Color.FromArgb(62,41,87),35))g.FillRectangle(fill,ClientRectangle);var size=Math.Max(20,Math.Min(80,Width/14f));using(var f=new System.Drawing.Font("Segoe UI",size,System.Drawing.FontStyle.Bold,System.Drawing.GraphicsUnit.Pixel))using(var b=new System.Drawing.SolidBrush(System.Drawing.Color.White))g.DrawString(Loc.T("Il tuo schermo.\nTutto lo spazio.","Your screen.\nAll the space."),f,b,new System.Drawing.RectangleF(Width*0.09f,Height*0.28f,Width*0.84f,Height*0.6f));using(var f=new System.Drawing.Font("Segoe UI",18,System.Drawing.GraphicsUnit.Pixel))using(var b=new System.Drawing.SolidBrush(System.Drawing.Color.LightGray))g.DrawString("OPENMIRROR · DEMO  "+DateTime.Now.ToString("HH:mm:ss"),f,b,Width*0.09f,Height*0.86f);}
    }
}
