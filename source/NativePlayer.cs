using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VCD300 {
sealed class GameDisplay:Control {
 Image image;Bitmap analog;bool dirty=true,lowQuality;string osd="";
 int[] sourcePixels,destPixels;bool menuMode=true;
 public bool MenuMode{get{return menuMode;}set{menuMode=value;Invalidate();}}
 public Image Image{get{return image;}set{image=value;FrameChanged();}}
 public bool LowQuality{get{return lowQuality;}set{if(lowQuality==value)return;lowQuality=value;FrameChanged();}}
 public string OsdText{get{return osd;}set{if(osd==value)return;osd=value;Invalidate();}}
 public void FrameChanged(){dirty=true;Invalidate();}
 public GameDisplay(){DoubleBuffered=true;BackColor=Color.Black;SetStyle(ControlStyles.Selectable,true);TabStop=false;}
 protected override void OnMouseDown(MouseEventArgs e){Focus();base.OnMouseDown(e);}
 void PrepareAnalog(){
  if(!dirty&&analog!=null)return;dirty=false;
  if(analog==null)analog=new Bitmap(256,240,PixelFormat.Format32bppArgb);
  using(Graphics g=Graphics.FromImage(analog)){g.InterpolationMode=InterpolationMode.Bilinear;g.DrawImage(image,new Rectangle(0,0,256,240));}
  Rectangle rect=new Rectangle(0,0,256,240);BitmapData data=analog.LockBits(rect,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
  try{
   if(sourcePixels==null){sourcePixels=new int[256*240];destPixels=new int[sourcePixels.Length];}
   int[] source=sourcePixels,dest=destPixels;Marshal.Copy(data.Scan0,source,0,source.Length);
   for(int y=0;y<240;y++)for(int x=0;x<256;x++){
    int i=y*256+x,row=y*256;
    int c0=source[row+(x>1?x-2:0)],c1=source[row+(x>0?x-1:0)],c2=source[i],c3=source[row+(x<255?x+1:255)],c4=source[row+(x<254?x+2:255)];
    int r=(((c0>>16)&255)+2*((c1>>16)&255)+4*((c2>>16)&255)+2*((c3>>16)&255)+((c4>>16)&255))/10;
    int g=(((c0>>8)&255)+2*((c1>>8)&255)+4*((c2>>8)&255)+2*((c3>>8)&255)+((c4>>8)&255))/10;
    int b=((c0&255)+2*(c1&255)+4*(c2&255)+2*(c3&255)+(c4&255))/10;
    int l1=77*((c1>>16)&255)+150*((c1>>8)&255)+29*(c1&255),l2=77*((c2>>16)&255)+150*((c2>>8)&255)+29*(c2&255),l3=77*((c3>>16)&255)+150*((c3>>8)&255)+29*(c3&255);
    int adjust=((l1+4*l2+l3)/6-(77*r+150*g+29*b))>>8;
    dest[i]=unchecked((int)0xff000000)|(Clamp(r+adjust)<<16)|(Clamp(g+adjust)<<8)|Clamp(b+adjust);
   }
   Marshal.Copy(dest,0,data.Scan0,dest.Length);
  }finally{analog.UnlockBits(data);}
 }
 static int Clamp(int value){return value<0?0:value>255?255:value;}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(Color.Black);if(image==null)return;
  int w=ClientSize.Width,h=ClientSize.Height;if(w<=0||h<=0)return;
  Image frame=image;if(lowQuality){PrepareAnalog();frame=analog;}
  e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;e.Graphics.PixelOffsetMode=PixelOffsetMode.Half;
  if(lowQuality&&menuMode){
   e.Graphics.DrawImage(image,new Rectangle(0,0,w,h),0,0,image.Width,image.Height,GraphicsUnit.Pixel);
   using(ImageAttributes attrs=new ImageAttributes()){ColorMatrix matrix=new ColorMatrix();matrix.Matrix33=.5f;attrs.SetColorMatrix(matrix);e.Graphics.DrawImage(frame,new Rectangle(0,0,w,h),0,0,frame.Width,frame.Height,GraphicsUnit.Pixel,attrs);}
  }else e.Graphics.DrawImage(frame,new Rectangle(0,0,w,h),0,0,frame.Width,frame.Height,GraphicsUnit.Pixel);
  if(osd.Length>0){
   if(lowQuality){
    using(Bitmap textLayer=new Bitmap(Math.Max(1,w/2),Math.Max(1,h/2),PixelFormat.Format32bppArgb)){
     using(Graphics g=Graphics.FromImage(textLayer))DrawOsd(g,textLayer.Width,textLayer.Height);
     e.Graphics.InterpolationMode=InterpolationMode.Bilinear;
     using(ImageAttributes attrs=new ImageAttributes()){ColorMatrix matrix=new ColorMatrix();matrix.Matrix33=.18f;attrs.SetColorMatrix(matrix);e.Graphics.DrawImage(textLayer,new Rectangle(2,0,w,h),0,0,textLayer.Width,textLayer.Height,GraphicsUnit.Pixel,attrs);}
     e.Graphics.DrawImage(textLayer,new Rectangle(0,0,w,h),0,0,textLayer.Width,textLayer.Height,GraphicsUnit.Pixel);
    }
   }else DrawOsd(e.Graphics,w,h);
  }
 }
 void DrawOsd(Graphics graphics,int w,int h){
  GraphicsState state=graphics.Save();float size=Math.Max(9.5f,h/18f);
  graphics.TranslateTransform(Math.Max(7,w*.035f),Math.Max(6,h*.04f));graphics.ScaleTransform(.76f,1);
  graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
  using(Font font=new Font("SimSun",size,FontStyle.Regular,GraphicsUnit.Pixel)){
   string[] lines=osd.Split(new char[]{'\n'},StringSplitOptions.None);for(int i=0;i<lines.Length;i++){float y=i*size*1.35f;graphics.DrawString(lines[i],font,Brushes.Black,1,y+1);graphics.DrawString(lines[i],font,Brushes.White,0,y);}
  }graphics.Restore(state);
 }
 protected override void Dispose(bool disposing){if(disposing&&analog!=null)analog.Dispose();base.Dispose(disposing);}
}
[StructLayout(LayoutKind.Sequential)] struct CoreStats {
 public uint frames,pc,reads,apu,jammed,samples,signalFrames,peak,hash;
}
static class Core {
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int vcd_load(byte[] rom,byte[] dir);
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int vcd_step(int a,int b);
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vcd_video();
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern int vcd_audio(short[] output,int capacity);
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void vcd_stats(out CoreStats stats);
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr vcd_error();
 [DllImport("vcdcore.dll",CallingConvention=CallingConvention.Cdecl)] internal static extern void vcd_close();
 internal static byte[] Utf8(string s){return Encoding.UTF8.GetBytes(s+"\0");}
}
sealed class VcdAudioFilter {
 double filtered;int phase;short held;
 internal void Process(short[] samples,int count){
  for(int i=0;i<count;i++){
   filtered+=.32*(samples[i]-filtered);
   if(phase==0)held=(short)Math.Max(short.MinValue,Math.Min(short.MaxValue,Math.Round(filtered/256.0)*256));
   samples[i]=held;phase=(phase+1)&3;
  }
 }
}
sealed class Player:IDisposable {
 readonly Thread thread;readonly object sync=new object();readonly byte[] pixels=new byte[245760];
 public volatile int P1,P2;public volatile bool Stop,Done,AudioDevice,Paused,Loaded,LowQuality;
 public string Error="";public CoreStats Stats;public int Volume=75;
 readonly string rom,dir;readonly bool test;
 public Player(string file,string directory,bool auto,bool paused=false){rom=file;dir=directory;test=auto;Paused=paused;thread=new Thread(Run);thread.IsBackground=true;thread.Name="VCD300 emulation";thread.Start();}
 void Run(){
  WaveOutput audio=null;bool loaded=false;
  try {
   if(Core.vcd_load(Core.Utf8(rom),Core.Utf8(dir))==0){Error=Marshal.PtrToStringAnsi(Core.vcd_error());return;}loaded=true;
   audio=new WaveOutput();AudioDevice=audio.Available;Loaded=true;
   VcdAudioFilter filter=new VcdAudioFilter();bool filteredLast=false;short[] samples=new short[4096];Stopwatch clock=Stopwatch.StartNew();uint frame=0;double nextAt=0;
   while(!Stop){
    if(Paused){Thread.Sleep(15);continue;}
    int a=P1,b=P2;if(test){a=frame>=600&&frame<630?8:0;if(frame>=900&&frame<960)a=129;b=0;}
    if(Core.vcd_step(a,b)==0){Error="核心停止运行";break;}
    CoreStats stats;Core.vcd_stats(out stats);frame=stats.frames;
    lock(sync){Marshal.Copy(Core.vcd_video(),pixels,0,pixels.Length);Stats=stats;}
    int count=Core.vcd_audio(samples,samples.Length);if(count>0){bool filtering=LowQuality;if(filtering){if(!filteredLast)filter=new VcdAudioFilter();filter.Process(samples,count);}filteredLast=filtering;for(int i=0;i<count;i++)samples[i]=(short)(samples[i]*Volume/100);audio.Write(samples,count);}
    if(stats.jammed!=0){Error="检测到CPU停滞，已停止本次运行。";break;}
    if(test&&frame>=1200)break;
    nextAt+=1000.0/60.0988;int delay=(int)(nextAt-clock.Elapsed.TotalMilliseconds);if(delay>0)Thread.Sleep(Math.Min(delay,40));
    // Avoid a long burst when the app resumes from suspension or a breakpoint.
    if(clock.Elapsed.TotalMilliseconds-nextAt>250)nextAt=clock.Elapsed.TotalMilliseconds;
   }
  }catch(Exception ex){Error=ex.Message;}
  finally {if(audio!=null)audio.Dispose();if(loaded)Core.vcd_close();Done=true;}
 }
 public CoreStats PaintFrame(Bitmap bitmap){
  lock(sync){BitmapData data=bitmap.LockBits(new Rectangle(0,0,256,240),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   try{Marshal.Copy(pixels,0,data.Scan0,pixels.Length);}finally{bitmap.UnlockBits(data);}return Stats;}
 }
 public void Dispose(){Stop=true;if(Thread.CurrentThread!=thread)thread.Join();}
}
sealed class WaveOutput:IDisposable {
 [StructLayout(LayoutKind.Sequential)] struct Format{public ushort tag,channels;public uint rate,bytes;public ushort align,bits,extra;}
 [StructLayout(LayoutKind.Sequential)] struct Header{public IntPtr data;public uint length,recorded;public IntPtr user;public uint flags,loops;public IntPtr next,reserved;}
 [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr h,int device,ref Format format,IntPtr callback,IntPtr instance,uint flags);
 [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr h,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr h);
 [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr h);
 IntPtr device=IntPtr.Zero;readonly IntPtr[] headers=new IntPtr[8],buffers=new IntPtr[8];int next=0;
 readonly uint size=(uint)Marshal.SizeOf(typeof(Header));readonly int flagsOffset=(int)Marshal.OffsetOf(typeof(Header),"flags");
 public bool Available{get{return device!=IntPtr.Zero;}}
 public WaveOutput(){
  if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return;
  Format f=new Format();f.tag=1;f.channels=1;f.rate=44100;f.bytes=88200;f.align=2;f.bits=16;
  if(waveOutOpen(out device,-1,ref f,IntPtr.Zero,IntPtr.Zero,0)!=0){device=IntPtr.Zero;return;}
  try {for(int i=0;i<headers.Length;i++){buffers[i]=Marshal.AllocHGlobal(8192);headers[i]=Marshal.AllocHGlobal((int)size);Header h=new Header();h.data=buffers[i];h.length=8192;Marshal.StructureToPtr(h,headers[i],false);if(waveOutPrepareHeader(device,headers[i],size)!=0)throw new IOException("音频缓冲区初始化失败");}}
  catch{Dispose();}
 }
 public void Write(short[] data,int count){if(!Available)return;IntPtr header=headers[next];if((Marshal.ReadInt32(header,flagsOffset)&16)!=0)return;
  Marshal.Copy(data,0,buffers[next],count);Marshal.WriteInt32(header,IntPtr.Size,count*2);waveOutWrite(device,header,size);next=(next+1)%headers.Length;
 }
 public void Dispose(){if(device==IntPtr.Zero)return;waveOutReset(device);for(int i=0;i<headers.Length;i++){if(headers[i]!=IntPtr.Zero){waveOutUnprepareHeader(device,headers[i],size);Marshal.FreeHGlobal(headers[i]);headers[i]=IntPtr.Zero;}if(buffers[i]!=IntPtr.Zero){Marshal.FreeHGlobal(buffers[i]);buffers[i]=IntPtr.Zero;}}waveOutClose(device);device=IntPtr.Zero;}
}
}
