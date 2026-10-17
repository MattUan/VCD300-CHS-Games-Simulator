using System;using System.IO;using System.Drawing;using System.Reflection;using System.Windows.Forms;
namespace VCD300 {static class ViewportTests {
 static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static void Paint(GameDisplay display,Bitmap target){using(Graphics g=Graphics.FromImage(target)){g.Clear(Color.Lime);typeof(GameDisplay).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(display,new object[]{new PaintEventArgs(g,new Rectangle(Point.Empty,target.Size))});}}
 [STAThread]static void Main(string[] args){Directory.CreateDirectory(args[0]);
  using(GameDisplay display=new GameDisplay())using(Bitmap source=new Bitmap(256,240)){
   using(Graphics g=Graphics.FromImage(source)){g.Clear(Color.Red);g.FillRectangle(Brushes.Blue,128,0,128,240);g.FillRectangle(Brushes.White,25,35,35,50);}display.Image=source;display.MenuMode=false;
   foreach(Size size in new[]{new Size(800,600),new Size(1280,720),new Size(1366,768),new Size(600,800)})foreach(bool soft in new[]{false,true}){
    display.Size=size;display.LowQuality=soft;Rectangle fit=WindowGeometry.Fit(size);
    using(Bitmap target=new Bitmap(size.Width,size.Height)){
     Paint(display,target);Check(target.GetPixel(fit.X+fit.Width/4,fit.Y+fit.Height/2).R>230,"Red quarter "+size);Check(target.GetPixel(fit.X+fit.Width*3/4,fit.Y+fit.Height/2).B>230,"Blue quarter "+size);
     for(int y=0;y<size.Height;y+=7)for(int x=0;x<size.Width;x+=7)if(!fit.Contains(x,y))Check(target.GetPixel(x,y).ToArgb()==Color.Black.ToArgb(),"Black bars "+size);
     display.OsdText="选择01\n请稍后\n正在下载";Paint(display,target);display.OsdText="";
     for(int x=0;x<fit.X;x++)Check(target.GetPixel(x,size.Height/2).ToArgb()==Color.Black.ToArgb(),"OSD stays inside image");
     if(size.Width==1280&&soft)target.Save(Path.Combine(args[0],"viewport-fullscreen.png"));
    }
   }
  }
  Console.WriteLine("ALL "+checks+" viewport pixel checks passed");
 }
}}
