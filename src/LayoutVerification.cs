using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenMirror {
    internal static class LayoutVerification {
        internal static int Run(string[] args){
            var output=args.Length>1?args[args.Length-1]:Path.Combine(Paths.Root,".build","layout");Directory.CreateDirectory(output);
            var report=new List<string>();
            try{
                var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                foreach(var lang in new[]{"it","en"}){
                    var main=new MainWindow(false,true);((ComboBox)typeof(MainWindow).GetField("language",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(main)).SelectedIndex=lang=="en"?1:0;Loc.Language=lang;typeof(MainWindow).GetMethod("ApplyLanguage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);typeof(MainWindow).GetMethod("UpdateUi",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);
                    if(Edition.AirPlayOnly){foreach(var field in new[]{"android","miracast","cast","chromeButton"}){var button=(Button)typeof(MainWindow).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(main);if(button.Parent!=null)throw new Exception("Unsupported protocol appears in AirPlay edition: "+field);}report.Add(lang+": AirPlay edition exposes only the AirPlay receiver.");}
                    foreach(var size in new[]{new Size(640,460),new Size(800,540),new Size(1140,720),new Size(1460,820)}){
                        Layout(main.RootLayout,size);VerifyText(main.RootLayout,report,lang+" "+size);Render(main.RootLayout,size,1.75,Path.Combine(output,"main-"+lang+"-"+(int)size.Width+".png"));
                    }
                    var record=(Button)typeof(MainWindow).GetField("record",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(main);
                    var video=(Border)main.RootLayout.Children.Cast<UIElement>().First(e=>e is Border&&Grid.GetRow(e)==3);
                    for(int width=640;width<=1200;width+=10){var size=new Size(width,650);record.Content=Loc.T("Registra","Record");Layout(main.RootLayout,size);double height=video.ActualHeight;record.Content=Loc.T("Ferma registrazione","Stop recording");Layout(main.RootLayout,size);if(Math.Abs(height-video.ActualHeight)>0.5)throw new Exception("Recording label changes video geometry at width "+width);}
                    record.Content=Loc.T("Registra","Record");report.Add(lang+": recording controls preserve video geometry at 57 window widths.");
                    var sessionField=typeof(MainWindow).GetField("session",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    sessionField.SetValue(main,new Session{Name=new string('W',60),Protocol=Edition.AirPlayOnly?"AirPlay":"ChromeCast",Demo=true});typeof(MainWindow).GetMethod("UpdateUi",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);
                    foreach(var size in new[]{new Size(640,460),new Size(800,540)}){Layout(main.RootLayout,size);VerifyText(main.RootLayout,report,"Receiver waiting "+lang+" "+size);Render(main.RootLayout,size,1.75,Path.Combine(output,(Edition.AirPlayOnly?"airplay":"chrome")+"-"+lang+"-"+(int)size.Width+".png"));}
                    sessionField.SetValue(main,null);typeof(MainWindow).GetMethod("UpdateUi",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(main,null);
                    main.SetFullscreen(true).GetAwaiter().GetResult();Layout(main.RootLayout,new Size(1280,720));
                    var stage=(Border)main.RootLayout.Children.Cast<UIElement>().First(e=>e is Border&&Grid.GetRowSpan(e)==5);if(stage.ActualWidth<1279||stage.ActualHeight<719||stage.Margin!=new Thickness(0))throw new Exception("Fullscreen stage does not fill the available area.");
                    report.Add(lang+": fullscreen fills 1280x720; chrome removed.");
                    var about=new AboutDialog();Layout(about.Body,new Size(590,610));VerifyText(about.Body,report,"About "+lang);Render(about.Body,new Size(590,610),1.75,Path.Combine(output,"about-"+lang+".png"));
                    var settings=new SettingsDialog(new Settings{Language=lang});Layout(settings.FormBody,new Size(540,1500));Render(settings.FormBody,new Size(540,1500),1.25,Path.Combine(output,"settings-"+lang+".png"));VerifyText(settings.FormBody,report,"Settings "+lang);
                }
                report.Add("PASS: bilingual adaptive layout, text metrics, 125/175% render and fullscreen sizing. No desktop windows were shown.");File.WriteAllLines(Path.Combine(output,"layout-report.txt"),report);return 0;
            }catch(Exception ex){report.Add("FAIL: "+ex);File.WriteAllLines(Path.Combine(output,"layout-report.txt"),report);return 1;}
        }
        static void Layout(FrameworkElement element,Size size){element.Measure(size);element.Arrange(new Rect(new Point(),size));element.UpdateLayout();}
        static IEnumerable<DependencyObject> Descendants(DependencyObject e){yield return e;for(int i=0;i<VisualTreeHelper.GetChildrenCount(e);i++)foreach(var c in Descendants(VisualTreeHelper.GetChild(e,i)))yield return c;}
        static void VerifyText(FrameworkElement root,List<string> report,string label){
            int n=0;foreach(var text in Descendants(root).OfType<TextBlock>()){
                if(text.Visibility!=Visibility.Visible||string.IsNullOrEmpty(text.Text)||text.ActualWidth<1||text.ActualHeight<1)continue;
                var formatted=new FormattedText(text.Text,CultureInfo.CurrentUICulture,text.FlowDirection,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,Brushes.White,1.0);
                formatted.MaxTextWidth=Math.Max(1,text.ActualWidth+2);if(text.TextWrapping!=TextWrapping.NoWrap&&formatted.Height>text.ActualHeight+2)throw new Exception(label+" clipped text: "+text.Text+" width="+text.ActualWidth+" height="+text.ActualHeight+" expected="+formatted.Height);
                n++;
            }report.Add(label+": "+n+" text elements fit.");
        }
        static void Render(FrameworkElement root,Size size,double scale,string path){var bmp=new RenderTargetBitmap((int)Math.Ceiling(size.Width*scale),(int)Math.Ceiling(size.Height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bmp.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))encoder.Save(f);}
    }
}
