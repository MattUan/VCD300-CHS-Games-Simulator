using System;using System.IO;using System.Drawing;using System.Diagnostics;using System.Windows.Forms;
namespace VCD300{static class TestDisplay{
 [STAThread]static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);
  using(GameDisplay display=new GameDisplay())using(Bitmap source=new Bitmap(256,240))using(Bitmap normal=new Bitmap(800,600))using(Bitmap soft=new Bitmap(800,600)){
   using(Graphics g=Graphics.FromImage(source)){g.Clear(Color.Red);g.FillRectangle(Brushes.Blue,128,0,128,240);g.FillRectangle(Brushes.White,30,70,30,30);}
   display.Size=new Size(800,600);display.CreateControl();display.Image=source;display.DrawToBitmap(normal,new Rectangle(0,0,800,600));
   if(normal.GetPixel(0,300).R<250||normal.GetPixel(799,300).B<250)throw new Exception("Unexpected side bars/crop");
   display.LowQuality=true;display.DrawToBitmap(soft,new Rectangle(0,0,800,600));
   int delta=0;for(int x=390;x<410;x++)if(normal.GetPixel(x,300)!=soft.GetPixel(x,300))delta++;if(delta<4)throw new Exception("VCD effect not applied");
   normal.Save(Path.Combine(dir,"filter-original.png"));soft.Save(Path.Combine(dir,"filter-vcd.png"));
   display.LowQuality=false;Stopwatch plain=Stopwatch.StartNew();for(int i=0;i<60;i++)display.DrawToBitmap(soft,new Rectangle(0,0,800,600));plain.Stop();display.LowQuality=true;Stopwatch sw=Stopwatch.StartNew();for(int i=0;i<60;i++){display.FrameChanged();display.DrawToBitmap(soft,new Rectangle(0,0,800,600));}sw.Stop();Console.WriteLine("PASS fill viewport, VCD softness and color bleed; 60 rendered frames in "+sw.ElapsedMilliseconds+" ms; original render "+plain.ElapsedMilliseconds+" ms");
  }
 }
}}
