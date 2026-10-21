param([string]$Destination = (Join-Path $PSScriptRoot '..\assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class OpenMirrorIcon {
 public static void Generate(string destination) {
  int[] sizes={16,24,32,48,64,128,256}; byte[][] images=new byte[sizes.Length][];
  for(int i=0;i<sizes.Length;i++) {
   using(var b=new Bitmap(sizes[i],sizes[i],PixelFormat.Format32bppArgb)) {
    using(var g=Graphics.FromImage(b)) {
     g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(sizes[i]/256f,sizes[i]/256f);
     using(var p=new GraphicsPath()) {p.AddArc(8,8,108,108,180,90);p.AddArc(140,8,108,108,270,90);p.AddArc(140,140,108,108,0,90);p.AddArc(8,140,108,108,90,90);p.CloseFigure();using(var fill=new LinearGradientBrush(new Rectangle(8,8,240,240),Color.FromArgb(36,61,87),Color.FromArgb(16,25,35),45))g.FillPath(fill,p);}
     using(var pen=new Pen(Color.FromArgb(80,220,190),13)) {pen.LineJoin=LineJoin.Round;g.DrawRectangle(pen,43,64,160,108);pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,123,174,123,199);g.DrawLine(pen,89,199,157,199);}
     using(var fill=new SolidBrush(Color.FromArgb(24,34,47)))g.FillRectangle(fill,164,111,51,91);
     using(var pen=new Pen(Color.FromArgb(164,153,255),10)){pen.LineJoin=LineJoin.Round;g.DrawRectangle(pen,164,111,51,91);}
     using(var pen=new Pen(Color.FromArgb(238,248,255),7)){pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLines(pen,new[]{new Point(96,112),new Point(115,94),new Point(97,94)});g.DrawLine(pen,115,94,115,111);g.DrawLines(pen,new[]{new Point(137,132),new Point(118,150),new Point(136,150)});g.DrawLine(pen,118,150,118,133);}
    }
    using(var stream=new MemoryStream()){b.Save(stream,ImageFormat.Png);images[i]=stream.ToArray();}
    b.Save(Path.Combine(destination,"openmirror-"+sizes[i]+".png"),ImageFormat.Png);
   }
  }
  using(var writer=new BinaryWriter(File.Create(Path.Combine(destination,"openmirror.ico")))) {
   writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
   foreach(var image in images)writer.Write(image);
  }
 }
}
'@
if (-not ('OpenMirrorIcon' -as [type])) { Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing }
[OpenMirrorIcon]::Generate([IO.Path]::GetFullPath($Destination))
Write-Host 'Icona generata: SVG sorgente, PNG 16–256 e ICO multirisoluzione.'
