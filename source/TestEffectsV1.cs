using System;using System.Drawing;using System.Drawing.Imaging;using System.IO;using System.Security.Cryptography;using System.Windows.Forms;
namespace VCD300{class Qa{
static string Hash(Bitmap b){using(MemoryStream m=new MemoryStream()){b.Save(m,ImageFormat.Png);return BitConverter.ToString(SHA256.Create().ComputeHash(m.ToArray()));}}
[STAThread]static void Main(string[] args){
using(GameDisplay d=new GameDisplay())using(Bitmap src=new Bitmap(args[0]))using(Bitmap game=new Bitmap(800,600)){
d.Size=new Size(800,600);d.CreateControl();d.Image=src;d.LowQuality=true;
d.MenuMode=false;
d.DrawToBitmap(game,new Rectangle(0,0,800,600));Console.WriteLine("game="+Hash(game));

d.MenuMode=true;using(Bitmap menu=new Bitmap(800,600))using(Bitmap plain=new Bitmap(800,600)){
d.DrawToBitmap(menu,new Rectangle(0,0,800,600));d.LowQuality=false;d.DrawToBitmap(plain,new Rectangle(0,0,800,600));long full=0,half=0;
for(int y=0;y<600;y+=3)for(int x=0;x<800;x+=3){Color a=plain.GetPixel(x,y),b=game.GetPixel(x,y),c=menu.GetPixel(x,y);full+=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);half+=Math.Abs(a.R-c.R)+Math.Abs(a.G-c.G)+Math.Abs(a.B-c.B);}
double ratio=(double)half/full;if(ratio<.46||ratio>.54)throw new Exception("menu blend "+ratio);Console.WriteLine("PASS menu effect ratio="+ratio.ToString("F3"));
menu.Save(Path.Combine(args[1],"menu-half.png"));d.LowQuality=true;d.OsdText="选择01\n请稍后\n正在下载";d.DrawToBitmap(menu,new Rectangle(0,0,800,600));menu.Save(Path.Combine(args[1],"osd-soft.png"));string soft=Hash(menu);d.LowQuality=false;d.DrawToBitmap(menu,new Rectangle(0,0,800,600));if(soft==Hash(menu))throw new Exception("OSD unchanged");}
double[] energy=new double[2];for(int n=0;n<2;n++){short[] samples=new short[44100];double freq=n==0?440:10000;for(int i=0;i<samples.Length;i++)samples[i]=(short)(12000*Math.Sin(2*Math.PI*freq*i/44100));VcdAudioFilter f=new VcdAudioFilter();f.Process(samples,samples.Length);for(int i=100;i<samples.Length;i++){if(samples[i]%256!=0)throw new Exception("audio quantization");energy[n]+=(double)samples[i]*samples[i];}}
if(energy[1]/energy[0]>.15)throw new Exception("audio high frequency not reduced");
short[] whole=new short[2048],a1=new short[777],a2=new short[1271];for(int i=0;i<whole.Length;i++)whole[i]=(short)((i*1237)%28000-14000);Array.Copy(whole,a1,777);Array.Copy(whole,777,a2,0,1271);new VcdAudioFilter().Process(whole,whole.Length);VcdAudioFilter split=new VcdAudioFilter();split.Process(a1,a1.Length);split.Process(a2,a2.Length);for(int i=0;i<whole.Length;i++)if(whole[i]!=(i<777?a1[i]:a2[i-777]))throw new Exception("audio buffer state");
Console.WriteLine("PASS audio high-frequency attenuation, quantization, continuous buffer state");

}}}}
