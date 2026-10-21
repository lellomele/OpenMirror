using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenMirror {
    internal static class Ui {
        public static readonly Brush Background=B("#10141C"),Surface=B("#181E28"),Card=B("#222B38"),Line=B("#354254"),Text=B("#F0F5FA"),Muted=B("#A2B2C7"),Accent=B("#50DCBE"),Danger=B("#FFADAA");
        public static Brush B(string color){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(color);b.Freeze();return b;}
        public static TextBlock Label(string text,double size=14,Brush color=null){return new TextBlock{Text=text,FontSize=size,Foreground=color??Text,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
        public static Button Button(string text,Action action,bool primary=false){var b=new Button{Content=text,MinHeight=38,MinWidth=80,Padding=new Thickness(16,8,16,8),Margin=new Thickness(0,0,8,8),FontSize=14,FontWeight=FontWeights.SemiBold,Cursor=System.Windows.Input.Cursors.Hand};b.Style=ButtonStyle(primary);b.Click+=(o,e)=>action();return b;}
        public static Style ButtonStyle(bool primary){
            var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.BackgroundProperty,primary?Accent:Card));style.Setters.Add(new Setter(Control.ForegroundProperty,primary?Background:Text));style.Setters.Add(new Setter(Control.BorderBrushProperty,Line));
            var factory=new FrameworkElementFactory(typeof(Border));factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));factory.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));factory.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));factory.SetValue(Border.BorderThicknessProperty,new Thickness(1));factory.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);factory.AppendChild(presenter);
            var template=new ControlTemplate(typeof(Button)){VisualTree=factory};style.Setters.Add(new Setter(Control.TemplateProperty,template));
            var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,0.83));style.Triggers.Add(hover);
            var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.4));style.Triggers.Add(disabled);return style;
        }
        public static void Style(Window w){w.Background=Background;w.Foreground=Text;w.FontFamily=new FontFamily("Segoe UI");w.FontSize=14;w.UseLayoutRounding=true;w.SnapsToDevicePixels=true;w.WindowStartupLocation=WindowStartupLocation.CenterOwner;
            var text=new Style(typeof(TextBox));text.Setters.Add(new Setter(Control.BackgroundProperty,Card));text.Setters.Add(new Setter(Control.ForegroundProperty,Text));text.Setters.Add(new Setter(Control.BorderBrushProperty,Line));text.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,7,10,7)));text.Setters.Add(new Setter(Control.FontSizeProperty,14.0));text.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,36.0));w.Resources[typeof(TextBox)]=text;
        }
        public static Border Box(UIElement child,Thickness? padding=null){return new Border{Background=Surface,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=padding??new Thickness(20),Child=child};}
        public static StackPanel Vertical(params UIElement[] children){var p=new StackPanel{Orientation=Orientation.Vertical};foreach(var c in children)p.Children.Add(c);return p;}
        public static TextBox Edit(string value){return new TextBox{Text=value??"",Margin=new Thickness(0,7,0,18)};}
        public static void FitWindow(Window w){var a=SystemParameters.WorkArea;var width=Math.Max(280,a.Width-24);var height=Math.Max(220,a.Height-24);w.MinWidth=Math.Min(w.MinWidth,width);w.MinHeight=Math.Min(w.MinHeight,height);w.Width=Math.Max(w.MinWidth,Math.Min(w.Width,width));w.Height=Math.Max(w.MinHeight,Math.Min(w.Height,height));w.MaxHeight=height;}
    }
}
