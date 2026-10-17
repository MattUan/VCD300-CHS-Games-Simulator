using System;using System.Drawing;
namespace VCD300 {static class GeometryTests {
 static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static void Main(){
  foreach(Size size in new[]{new Size(1920,1080),new Size(1366,768),new Size(1280,1024),new Size(800,600),new Size(600,800),new Size(1031,701),new Size(0,0),new Size(3,2)}){
   Rectangle fit=WindowGeometry.Fit(size);Check(fit.Width*3==fit.Height*4,"Fit aspect "+size);Check(fit.X>=0&&fit.Y>=0&&fit.Right<=size.Width&&fit.Bottom<=size.Height,"Fit bounds "+size);Check(fit.IsEmpty||Math.Abs(fit.Left-(size.Width-fit.Right))<=1&&Math.Abs(fit.Top-(size.Height-fit.Bottom))<=1,"Fit center "+size);
  }
  foreach(Size frame in new[]{new Size(16,39),new Size(24,58)})foreach(int footer in new[]{132,198})foreach(Size wanted in new[]{new Size(1027,701),new Size(10,10),new Size(2000,1900)})for(int edge=1;edge<=8;edge++){
   Rectangle previous=new Rectangle(100,90,896,831),request=new Rectangle(70,60,wanted.Width,wanted.Height);Size minimum=new Size(600,610);
   Rectangle r=WindowGeometry.Constrain(request,previous,edge,frame,footer,minimum);
   Check((r.Width-frame.Width)*3==(r.Height-frame.Height-footer)*4,"Resize aspect "+edge);Check(r.Width>=minimum.Width&&r.Height>=minimum.Height,"Minimum "+edge);
   Check((edge==1||edge==4||edge==7)?r.Right==request.Right:r.Left==request.Left,"Horizontal anchor "+edge);
   Check((edge==3||edge==4||edge==5)?r.Bottom==request.Bottom:r.Top==request.Top,"Vertical anchor "+edge);
  }
  Console.WriteLine("ALL "+checks+" geometry checks passed");
 }
}}
