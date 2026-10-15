using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;

[assembly:System.Reflection.AssemblyTitle("中文游戏300")]
[assembly:System.Reflection.AssemblyCompany("MattUan")]
[assembly:System.Reflection.AssemblyProduct("中文游戏300")]
[assembly:System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly:System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly:System.Reflection.AssemblyInformationalVersion("1.0 beta")]

namespace VCD300 {
static class Program {
 internal const string Version="V1.0 beta";
 [STAThread] static void Main(string[] args) {
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try {
   Menu menu=new Menu();
   if(args.Contains("--smoke-test"))menu.Shown+=delegate{menu.Smoke();};
   if(args.Contains("--integration-test"))menu.Shown+=delegate{string arg=args.FirstOrDefault(x=>x.StartsWith("--integration-game="));int n=3;if(arg!=null)int.TryParse(arg.Substring(19),out n);menu.Integration(n,args.Contains("--vcd-quality"));};
   Application.Run(menu);
  }catch(Exception ex){MessageBox.Show("程序未能启动：\n"+ex.Message,"中文游戏300");}
 }
}
static class Native {
 public static int[] LegacyButtons={1,2,9,10};
 internal static int PollCalls;
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] static extern uint XI(uint index,out XState state);
 [DllImport("winmm.dll")] static extern uint joyGetPosEx(uint index,ref JoyInfo info);
 [StructLayout(LayoutKind.Sequential)] struct XState{public uint packet;public ushort buttons;public byte lt,rt;public short lx,ly,rx,ry;}
 [StructLayout(LayoutKind.Sequential)] struct JoyInfo{public uint size,flags,x,y,z,r,u,v,buttons,number,pov,res1,res2;}
 internal static bool Down(Keys k){PollCalls++;return (GetAsyncKeyState((int)k)&0x8000)!=0;}
 internal static int Pad(int index,out string kind){
  PollCalls++;kind="未连接";XState s;
  try{if(XI((uint)index,out s)==0){
   kind="XInput";int b=0;ushort x=s.buttons;
   if((x&0x1000)!=0)b|=1;if((x&0x2000)!=0)b|=2;if((x&0x20)!=0)b|=4;if((x&0x10)!=0)b|=8;
   if((x&1)!=0||s.ly>16000)b|=16;if((x&2)!=0||s.ly< -16000)b|=32;
   if((x&4)!=0||s.lx< -16000)b|=64;if((x&8)!=0||s.lx>16000)b|=128;return b;
  }}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  JoyInfo j=new JoyInfo();j.size=(uint)Marshal.SizeOf(typeof(JoyInfo));j.flags=0xff;
  if(joyGetPosEx((uint)index,ref j)!=0)return 0;kind="USB 手柄";int v=0;
  for(int k=0;k<4;k++)if((j.buttons&(1u<<(LegacyButtons[k]-1)))!=0)v|=1<<k;
  if(j.x<16000)v|=64;if(j.x>49000)v|=128;if(j.y<16000)v|=16;if(j.y>49000)v|=32;
  if(j.pov!=0xffff&&j.pov<=36000){if(j.pov>=31500||j.pov<=4500)v|=16;if(j.pov>=4500&&j.pov<=13500)v|=128;if(j.pov>=13500&&j.pov<=22500)v|=32;if(j.pov>=22500&&j.pov<=31500)v|=64;}
  return v;
 }
}
// Keep input-method changes scoped to our UI thread and restore on exit/focus loss.
sealed class GameImeScope:IDisposable {
 readonly Control host,surface;readonly ImeMode hostMode,surfaceMode;readonly InputLanguage saved;
 bool disposed;
 internal GameImeScope(Control owner,Control screen){host=owner;surface=screen;hostMode=owner.ImeMode;surfaceMode=screen.ImeMode;saved=InputLanguage.CurrentInputLanguage;Apply();}
 internal void Apply(){if(disposed)return;host.ImeMode=ImeMode.Disable;surface.ImeMode=ImeMode.Disable;try{foreach(InputLanguage lang in InputLanguage.InstalledInputLanguages)if(lang.Culture.TwoLetterISOLanguageName=="en"){InputLanguage.CurrentInputLanguage=lang;break;}}catch{}}
 internal void RestoreLanguage(){if(saved!=null)try{InputLanguage.CurrentInputLanguage=saved;}catch{}}
 public void Dispose(){if(disposed)return;disposed=true;RestoreLanguage();host.ImeMode=hostMode;surface.ImeMode=surfaceMode;}
}
sealed class StableLabel:Label {
 internal StableLabel(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TextAlign=ContentAlignment.MiddleCenter;Dock=DockStyle.Fill;}
}
sealed class Menu:Form {
 readonly string baseDir=AppDomain.CurrentDomain.BaseDirectory;
 readonly string userDir=Environment.GetEnvironmentVariable("VCD300_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VCD300");
 readonly GameDisplay picture=new GameDisplay();readonly StableLabel status=new StableLabel(),pads=new StableLabel();
 readonly Panel overlay=new Panel(),buttonHost=new Panel();readonly FlowLayoutPanel buttons=new FlowLayoutPanel();readonly Timer timer=new Timer();
 readonly string[] names={"A","B","SELECT","START","↑","↓","←","→"};
 Keys[] key1={Keys.F,Keys.D,Keys.S,Keys.Enter,Keys.Up,Keys.Down,Keys.Left,Keys.Right};
 Keys[] key2={Keys.NumPad1,Keys.NumPad3,Keys.NumPad0,Keys.Decimal,Keys.NumPad8,Keys.NumPad2,Keys.NumPad4,Keys.NumPad6};
 bool mirror,lowQuality=true,full,integration,loading,armInput=true,forceBackgroundForTest;
 int page=1,slot=1,previousPad;string type1="未连接",type2="未连接",runtime="";
 Player game;Bitmap gameBitmap;GameImeScope ime;uint displayedFrame;DateTime loadingStarted,loadingUntil,chord=DateTime.MinValue,lastInfo=DateTime.MinValue;
 CheckBox qualityBox;NumericUpDown[] usbBoxes;Button saveOptions,cancelOptions;TableLayoutPanel optionContent;
 internal Menu(){
  Text="【中文游戏300 · "+Program.Version+"】";BackColor=Color.FromArgb(15,18,23);ForeColor=Color.White;
  Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
  ClientSize=new Size(880,792);MinimumSize=new Size(600,560);KeyPreview=true;
  Directory.CreateDirectory(userDir);LoadPreferences();picture.LowQuality=lowQuality;
  TableLayoutPanel layout=new TableLayoutPanel();layout.Dock=DockStyle.Fill;layout.ColumnCount=1;layout.RowCount=4;layout.Margin=Padding.Empty;
  layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,54));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));Controls.Add(layout);
  Panel viewport=new Panel();viewport.Dock=DockStyle.Fill;viewport.Margin=Padding.Empty;layout.Controls.Add(viewport,0,0);
  picture.Dock=DockStyle.Fill;picture.Margin=Padding.Empty;viewport.Controls.Add(picture);
  overlay.Dock=DockStyle.Fill;overlay.BackColor=BackColor;overlay.Visible=false;viewport.Controls.Add(overlay);
  status.ForeColor=Color.FromArgb(244,220,167);status.Margin=Padding.Empty;layout.Controls.Add(status,0,1);
  buttonHost.Dock=DockStyle.Fill;buttonHost.Margin=Padding.Empty;layout.Controls.Add(buttonHost,0,2);
  buttons.AutoSize=true;buttons.AutoSizeMode=AutoSizeMode.GrowAndShrink;buttons.WrapContents=false;buttons.Margin=Padding.Empty;buttons.Padding=Padding.Empty;buttonHost.Controls.Add(buttons);
  buttons.Controls.Add(Btn("− 上页",delegate{Turn(-1);}));buttons.Controls.Add(Btn("+ 下页",delegate{Turn(1);}));
  buttons.Controls.Add(Btn("选项",delegate{Options();}));buttons.Controls.Add(Btn("关于",delegate{About();}));buttons.Controls.Add(Btn("返回菜单",delegate{ReturnMenu();}));
  buttonHost.Resize+=delegate{CenterButtons();};buttons.SizeChanged+=delegate{CenterButtons();};
  pads.ForeColor=Color.Silver;pads.Margin=Padding.Empty;layout.Controls.Add(pads,0,3);
  KeyDown+=MenuKey;FormClosing+=delegate{timer.Stop();FinishGame();};
  Deactivate+=delegate{ClearInput();armInput=true;if(game!=null)game.Paused=true;if(ime!=null)ime.RestoreLanguage();UpdateInfo(false);};
  Activated+=delegate{armInput=true;previousPad=0;if(ime!=null)ime.Apply();};
  timer.Interval=16;timer.Tick+=Tick;timer.Start();RenderMenu();CenterButtons();
 }
 void CenterButtons(){buttons.Location=new Point(Math.Max(0,(buttonHost.ClientSize.Width-buttons.Width)/2),Math.Max(0,(buttonHost.ClientSize.Height-buttons.Height)/2));}
 Button Btn(string text,EventHandler action){Button b=new Button();b.Text=text;b.Size=new Size(90,34);b.Margin=new Padding(4,2,4,2);b.FlatStyle=FlatStyle.Flat;b.BackColor=Color.FromArgb(31,38,48);b.ForeColor=Color.White;b.TabStop=false;b.Click+=action;return b;}
 static void SetText(Label label,string value){if(label.Text!=value)label.Text=value;}
 bool Foreground{get{return !forceBackgroundForTest&&ContainsFocus&&Form.ActiveForm==this&&Native.GetForegroundWindow()==Handle;}}
 void RenderMenu(){
  if(game!=null)return;picture.MenuMode=true;Image old=picture.Image;using(var stream=File.OpenRead(Path.Combine(baseDir,"menus",page.ToString("00")+".png")))picture.Image=new Bitmap(stream);if(old!=null)old.Dispose();
  SetText(status,string.Format("第 {0:00} / 30 页 · 1–9 选游戏，0=10（本页第10项） · + / − 翻页 · F11 全屏",page));UpdateInfo(Foreground);
 }
 void Turn(int delta){if(game!=null||overlay.Visible)return;page=(page-1+delta+30)%30+1;RenderMenu();}
 void MenuKey(object sender,KeyEventArgs e){
  if(!Foreground&&!integration)return;
  if(e.KeyCode==Keys.F11){ToggleFull();e.Handled=true;e.SuppressKeyPress=true;return;}
  if(e.KeyCode==Keys.Escape&&overlay.Visible){CloseOverlay();e.Handled=true;e.SuppressKeyPress=true;return;}
  if(game!=null||overlay.Visible)return;int digit=-1;
  if(e.KeyCode>=Keys.D0&&e.KeyCode<=Keys.D9)digit=(int)e.KeyCode-(int)Keys.D0;
  if(e.KeyCode>=Keys.NumPad0&&e.KeyCode<=Keys.NumPad9)digit=(int)e.KeyCode-(int)Keys.NumPad0;
  if(digit>=0){slot=digit==0?10:digit;Launch();}
  else if(e.KeyCode==Keys.Oemplus||e.KeyCode==Keys.Add||e.KeyCode==Keys.PageDown)Turn(1);
  else if(e.KeyCode==Keys.OemMinus||e.KeyCode==Keys.Subtract||e.KeyCode==Keys.PageUp)Turn(-1);
  else if(e.KeyCode==Keys.Up){slot=(slot+8)%10+1;UpdateInfo(true);}
  else if(e.KeyCode==Keys.Down){slot=slot%10+1;UpdateInfo(true);}
  else if(e.KeyCode==Keys.Enter)Launch();
  else if(e.KeyCode==Keys.Escape&&full)ToggleFull();else return;
  e.Handled=true;e.SuppressKeyPress=true;
 }
 void ToggleFull(){full=!full;FormBorderStyle=full?FormBorderStyle.None:FormBorderStyle.Sizable;WindowState=full?FormWindowState.Maximized:FormWindowState.Normal;}
 void PrepareRuntime(){runtime=Path.Combine(userDir,"data");Directory.CreateDirectory(runtime);foreach(string f in new[]{"sav","fcs","cheats","movies","snaps"})Directory.CreateDirectory(Path.Combine(runtime,f));}
 void Launch(){
  if(game!=null||overlay.Visible)return;int n=(page-1)*10+slot;string rom=Path.Combine(baseDir,"roms","G"+n.ToString("000")+".nes");
  if(!File.Exists(rom)){SetText(status,"未找到 G"+n.ToString("000")+" 的游戏文件，请完整解压程序包。");return;}
  try{
   PrepareRuntime();ime=new GameImeScope(this,picture);gameBitmap=new Bitmap(256,240,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
   loading=true;loadingStarted=DateTime.UtcNow;loadingUntil=DateTime.UtcNow.AddMilliseconds(2000);picture.OsdText="选择"+slot.ToString("00")+"\n请稍后\n正在下载";
   displayedFrame=0;armInput=true;chord=DateTime.MinValue;game=new Player(rom,runtime,integration,true);game.LowQuality=lowQuality;SetText(status,"G"+n.ToString("000")+" · 请稍后");picture.Focus();
  }catch(Exception ex){FinishGame();RenderMenu();SetText(status,"启动失败："+ex.Message);}
 }
 int Keyboard(Keys[] map){int bits=0;for(int i=0;i<8;i++)if(Native.Down(map[i]))bits|=1<<i;return bits;}
 void ClearInput(){if(game!=null){game.P1=0;game.P2=0;}chord=DateTime.MinValue;previousPad=0;}
 void Tick(object sender,EventArgs e){
  bool foreground=Foreground;int p1=0,p2=0;
  // Do not poll either global keyboard state or gamepad state in the background.
  if(foreground&&!overlay.Visible){p1=Native.Pad(0,out type1);p2=Native.Pad(1,out type2);}
  if(game==null){
   if(foreground&&!overlay.Visible){
    if(armInput){if((p1|p2)==0)armInput=false;previousPad=p1;}
    else{int edge=p1&~previousPad;previousPad=p1;if((edge&64)!=0)Turn(-1);if((edge&128)!=0)Turn(1);if((edge&16)!=0){slot=(slot+8)%10+1;}if((edge&32)!=0){slot=slot%10+1;}if((edge&9)!=0)Launch();}
   }else{previousPad=0;armInput=true;}
   RefreshInfo(foreground);return;
  }
  if(game.Done){
   string error=game.Error;bool ready=game.Stats.frames>=1200;FinishGame();RenderMenu();
   if(error.Length>0)SetText(status,"运行发生错误："+error);
   if(integration){File.WriteAllText(Path.Combine(userDir,"integration-result.txt"),ready&&error.Length==0?"PASS: native gameplay, same window, return to menu, IME scope restored":"FAIL: "+error);Close();}return;
  }
  if(loading){
   if(game.Loaded&&DateTime.UtcNow>=loadingUntil){if(integration)File.WriteAllText(Path.Combine(userDir,"loading-ms.txt"),(DateTime.UtcNow-loadingStarted).TotalMilliseconds.ToString("F0"));loading=false;picture.OsdText="";picture.MenuMode=false;Image old=picture.Image;picture.Image=gameBitmap;if(old!=null)old.Dispose();}
   else{game.Paused=true;ClearInput();if(integration&&game.Loaded&&!File.Exists(Path.Combine(userDir,"osd-loading.png")))SaveWindow("osd-loading.png");RefreshInfo(foreground);return;}
  }
  bool accept=foreground&&!overlay.Visible;game.Paused=overlay.Visible||(!foreground&&!integration);
  if(accept){
   int a=Keyboard(key1)|p1,b=Keyboard(key2)|p2;
   if(armInput){if((a|b)==0)armInput=false;a=b=0;}
   if(mirror&&type2=="未连接")b|=a;game.P1=a;game.P2=b;
   if(Native.Down(Keys.Escape)){game.Stop=true;ClearInput();}
   if((p1&12)==12||(p2&12)==12){if(chord==DateTime.MinValue)chord=DateTime.UtcNow;if((DateTime.UtcNow-chord).TotalMilliseconds>=800){game.Stop=true;ClearInput();}}else chord=DateTime.MinValue;
  }else{ClearInput();armInput=true;}
  CoreStats stats=game.Stats;if(stats.frames!=displayedFrame){stats=game.PaintFrame(gameBitmap);displayedFrame=stats.frames;picture.FrameChanged();}
  if(integration&&stats.frames>=1050&&!File.Exists(Path.Combine(userDir,"host-game.png"))){gameBitmap.Save(Path.Combine(userDir,"embedded-game.png"));SaveWindow("host-game.png");}
  RefreshInfo(foreground);
 }
 void RefreshInfo(bool foreground){if((DateTime.UtcNow-lastInfo).TotalMilliseconds<250)return;lastInfo=DateTime.UtcNow;UpdateInfo(foreground);}
 void UpdateInfo(bool foreground){
  string quality=lowQuality?"仿 VCD 效果":"原始画质";
  if(game==null)SetText(pads,"当前 G"+((page-1)*10+slot).ToString("000")+" · 手柄1："+type1+" · 手柄2："+type2+" · "+quality);
  else{
   SetText(pads,"手柄1："+type1+" · 手柄2："+type2+" · "+quality+(!foreground?" · 已暂停":""));
   if(!loading)SetText(status,"G"+((page-1)*10+slot).ToString("000")+" · Esc 返回菜单 · F11 全屏"+(!foreground?" · 窗口未激活":""));
  }
 }
 void ReturnMenu(){if(game!=null){game.Stop=true;ClearInput();}CloseOverlay();if(game==null)RenderMenu();}
 void FinishGame(){ClearInput();if(game!=null){game.Dispose();game=null;}if(picture.Image==gameBitmap)picture.Image=null;if(gameBitmap!=null){gameBitmap.Dispose();gameBitmap=null;}loading=false;picture.OsdText="";if(ime!=null){ime.Dispose();ime=null;}armInput=true;}
 void LoadPreferences(){
  try{string[] lines=File.ReadAllLines(Path.Combine(userDir,"keys.txt"));if(lines.Length>=17){Keys[] one=new Keys[8],two=new Keys[8];for(int i=0;i<8;i++){one[i]=(Keys)int.Parse(lines[i]);two[i]=(Keys)int.Parse(lines[i+8]);}if(one.Distinct().Count()==8&&two.Distinct().Count()==8){key1=one;key2=two;mirror=lines[16]=="1";}}}catch{}
  try{int[] ids=File.ReadAllLines(Path.Combine(userDir,"pad.txt")).Select(int.Parse).ToArray();if(ids.Length==4&&ids.All(x=>x>=1&&x<=32)&&ids.Distinct().Count()==4)Native.LegacyButtons=ids;}catch{}
  try{lowQuality=File.ReadAllText(Path.Combine(userDir,"display.txt")).Trim()=="vcd=1";}catch{}
 }
 void SavePreferences(Keys[] one,Keys[] two,bool sync,int[] ids,bool vcd){
  List<string> lines=new List<string>();lines.AddRange(one.Select(k=>((int)k).ToString()));lines.AddRange(two.Select(k=>((int)k).ToString()));lines.Add(sync?"1":"0");
  File.WriteAllLines(Path.Combine(userDir,"keys.txt"),lines.ToArray());File.WriteAllLines(Path.Combine(userDir,"pad.txt"),ids.Select(x=>x.ToString()).ToArray());File.WriteAllText(Path.Combine(userDir,"display.txt"),vcd?"vcd=1":"vcd=0");
  key1=one;key2=two;mirror=sync;Native.LegacyButtons=ids;lowQuality=vcd;picture.LowQuality=vcd;if(game!=null)game.LowQuality=vcd;
 }
 void OpenOverlay(){ClearInput();if(game!=null)game.Paused=true;foreach(Control c in overlay.Controls.Cast<Control>().ToArray())c.Dispose();overlay.Controls.Clear();overlay.Visible=true;overlay.BringToFront();picture.Visible=false;}
 void CloseOverlay(){overlay.Visible=false;picture.Visible=true;armInput=true;previousPad=0;if(game!=null)game.Paused=loading||(!Foreground&&!integration);picture.Focus();UpdateInfo(Foreground);}
 Label TextLabel(string text,int height){Label l=new Label();l.Text=text;l.Dock=DockStyle.Fill;l.AutoSize=false;l.Height=height;l.TextAlign=ContentAlignment.MiddleLeft;l.Margin=new Padding(0,4,0,4);return l;}
 TableLayoutPanel Stack(){TableLayoutPanel t=new TableLayoutPanel();t.ColumnCount=1;t.AutoSize=true;t.AutoSizeMode=AutoSizeMode.GrowAndShrink;t.Dock=DockStyle.Top;t.Margin=Padding.Empty;t.Padding=new Padding(18,12,18,16);return t;}
 void AddRow(TableLayoutPanel t,Control c,int height){int row=t.RowCount++;t.RowStyles.Add(new RowStyle(height<0?SizeType.AutoSize:SizeType.Absolute,Math.Max(0,height)));c.Dock=DockStyle.Fill;t.Controls.Add(c,0,row);}
 void Options(){
  OpenOverlay();Keys[] one=(Keys[])key1.Clone(),two=(Keys[])key2.Clone();
  Panel actions=new Panel();actions.Dock=DockStyle.Bottom;actions.Height=54;overlay.Controls.Add(actions);
  Panel scroll=new Panel();scroll.Dock=DockStyle.Fill;scroll.AutoScroll=true;overlay.Controls.Add(scroll);scroll.BringToFront();
  optionContent=Stack();scroll.Controls.Add(optionContent);Label heading=TextLabel("【选项】",34);heading.Font=new Font(Font.FontFamily,15);AddRow(optionContent,heading,42);
  AddRow(optionContent,TextLabel("点击键位按钮后按下新按键；Esc、F11 为程序保留键。",30),36);
  TableLayoutPanel keys=new TableLayoutPanel();keys.ColumnCount=3;keys.RowCount=9;keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));
  keys.Controls.Add(TextLabel("操作说明",24),0,0);keys.Controls.Add(TextLabel("玩家1",24),1,0);keys.Controls.Add(TextLabel("玩家2",24),2,0);keys.RowStyles.Add(new RowStyle(SizeType.Absolute,30));
  Label message=TextLabel("",42);message.ForeColor=Color.Gold;
  Button listening=null;
  for(int i=0;i<8;i++){int index=i;keys.RowStyles.Add(new RowStyle(SizeType.Absolute,38));keys.Controls.Add(TextLabel(names[i],32),0,i+1);
   for(int p=0;p<2;p++){int player=p;Keys[] map=p==0?one:two;Button b=Btn(map[i].ToString(),delegate{});b.Dock=DockStyle.Fill;b.ImeMode=ImeMode.Disable;b.Margin=new Padding(4,3,4,3);keys.Controls.Add(b,p+1,i+1);
    b.Click+=delegate{if(listening!=null)listening.Text="请重新选择";listening=b;b.Text="请按新按键";b.Focus();};
    b.KeyDown+=delegate(object o,KeyEventArgs ev){if(listening!=b)return;ev.Handled=true;ev.SuppressKeyPress=true;if(ev.KeyCode==Keys.Escape||ev.KeyCode==Keys.F11){message.Text="该键由程序保留，请使用其他按键。";return;}map[index]=ev.KeyCode;b.Text=ev.KeyCode.ToString();listening=null;message.Text="";};
   }
  }AddRow(optionContent,keys,342);
  CheckBox sync=new CheckBox();sync.Text="兼容模式：没有第二只手柄时，将玩家1同时发送到端口2";sync.Checked=mirror;sync.AutoSize=true;AddRow(optionContent,sync,40);
  qualityBox=new CheckBox();qualityBox.Text="仿 VCD 效果（画面柔化、降低音质，默认开启）";qualityBox.Checked=lowQuality;qualityBox.AutoSize=true;AddRow(optionContent,qualityBox,40);
  AddRow(optionContent,TextLabel("传统 USB 手柄按钮编号（1–32）",30),38);
  TableLayoutPanel padGrid=new TableLayoutPanel();padGrid.ColumnCount=4;padGrid.RowCount=2;usbBoxes=new NumericUpDown[4];
  for(int i=0;i<4;i++){padGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));Label caption=TextLabel(names[i],26);caption.Margin=new Padding(6,0,6,0);padGrid.Controls.Add(caption,i,0);NumericUpDown number=new NumericUpDown();number.Minimum=1;number.Maximum=32;number.Value=Native.LegacyButtons[i];number.Dock=DockStyle.Fill;number.Margin=new Padding(6,3,6,3);number.TextAlign=HorizontalAlignment.Center;usbBoxes[i]=number;padGrid.Controls.Add(number,i,1);}
  padGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,28));padGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,36));AddRow(optionContent,padGrid,72);
  AddRow(optionContent,TextLabel("XInput：A/B、BACK=SELECT、START=START。\n游戏内同时按住 SELECT+START 约1秒返回菜单。",52),58);AddRow(optionContent,message,48);
  FlowLayoutPanel row=new FlowLayoutPanel();row.AutoSize=true;row.WrapContents=false;actions.Controls.Add(row);
  saveOptions=Btn("保存并返回",delegate{
   if(one.Distinct().Count()!=8||two.Distinct().Count()!=8){message.Text="同一玩家的八个按键不能重复。";scroll.ScrollControlIntoView(message);return;}
   int[] ids=usbBoxes.Select(x=>(int)x.Value).ToArray();if(ids.Distinct().Count()!=4){message.Text="四个 USB 手柄按钮编号不能重复。";return;}
   try{SavePreferences(one,two,sync.Checked,ids,qualityBox.Checked);CloseOverlay();}catch(Exception ex){message.Text="保存失败："+ex.Message;}
  });saveOptions.Width=132;cancelOptions=Btn("取消",delegate{CloseOverlay();});cancelOptions.Width=100;row.Controls.Add(saveOptions);row.Controls.Add(cancelOptions);
  EventHandler center=delegate{row.Location=new Point(Math.Max(0,(actions.ClientSize.Width-row.Width)/2),8);};actions.Resize+=center;row.SizeChanged+=center;center(null,EventArgs.Empty);
 }
 const string AboutText="【中文游戏300 · V1.0 beta】\r\n\r\n【开发者】\r\n开发者：MattUan\r\nGitHub：https://github.com/MattUan\r\n\r\n【游戏来源】\r\n本程序全部游戏 ROM 与原菜单提取自《中文游戏300》VCD光盘。300个菜单选项对应150款不同游戏。\r\n\r\n【开源与许可】\r\n模拟器内核基于 FCEUX 2.7.0，版权归 FCEUX 团队及其贡献者所有，遵循 GNU GPL v2 或更新版本（GPL-2.0-or-later）。本程序亦按 GPL-2.0-or-later 提供源码。完整许可和第三方版权声明见 licenses，源代码见 source。代码计划在开发者的 GitHub 仓库公开。\r\n开源软件许可不代表游戏 ROM 与原盘素材的再分发授权。\r\n\r\n【操作说明】\r\n1–9 选本页游戏，0=10（本页第10项）；+ / − 翻页；方向键移动；F=A，D=B，S=SELECT，Enter=START；Esc 返回菜单；F11 全屏。\r\n部分游戏需要先选择难度或选关后，再次按 START 或 A 确认。\r\n仿 VCD 效果默认开启，菜单强度为游戏画面的50%，同时降低游戏音质；可在【选项】中关闭。\r\n\r\n【版本测试】\r\n当前版本为测试版，欢迎大家积极参与测试，如遇bug欢迎在github中提交Issues。";
 void About(){OpenOverlay();Panel footer=new Panel();footer.Dock=DockStyle.Bottom;footer.Height=52;overlay.Controls.Add(footer);Button back=Btn("返回",delegate{CloseOverlay();});footer.Controls.Add(back);EventHandler center=delegate{back.Location=new Point(Math.Max(0,(footer.ClientSize.Width-back.Width)/2),8);};footer.Resize+=center;footer.Layout+=delegate{center(null,EventArgs.Empty);};center(null,EventArgs.Empty);Panel body=new Panel();body.Dock=DockStyle.Fill;body.Padding=new Padding(22,18,22,6);overlay.Controls.Add(body);body.BringToFront();TextBox text=new TextBox();text.Multiline=true;text.ReadOnly=true;text.ScrollBars=ScrollBars.Vertical;text.BorderStyle=BorderStyle.None;text.BackColor=BackColor;text.ForeColor=ForeColor;text.Font=Font;text.Dock=DockStyle.Fill;text.Text=AboutText;body.Controls.Add(text);}
 void SaveWindow(string name){PerformLayout();overlay.PerformLayout();Update();using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(Path.Combine(userDir,name));}}
 internal void Integration(int n,bool vcd){if(n<1||n>300)n=3;integration=true;lowQuality=vcd;picture.LowQuality=vcd;if(game!=null)game.LowQuality=vcd;page=(n-1)/10+1;slot=(n-1)%10+1;RenderMenu();Launch();}
 internal void Smoke(){
  timer.Stop();List<string> checks=new List<string>();PerformLayout();CenterButtons();if(Math.Abs(buttons.Left*2+buttons.Width-buttonHost.ClientSize.Width)>2)throw new Exception("Buttons not centered");checks.Add("PASS centered footer");
  bool original=lowQuality;if(!File.Exists(Path.Combine(userDir,"display.txt"))&&!original)throw new Exception("VCD default disabled");checks.Add("PASS VCD enabled by default for new settings");Options();if(Application.OpenForms.Count!=1)throw new Exception("Options opened a form");SaveWindow("options.png");((ScrollableControl)optionContent.Parent).AutoScrollPosition=new Point(0,400);SaveWindow("options-usb.png");qualityBox.Checked=!original;cancelOptions.PerformClick();if(lowQuality!=original)throw new Exception("Cancel changed preferences");
  Options();qualityBox.Checked=true;saveOptions.PerformClick();if(!lowQuality||!picture.LowQuality)throw new Exception("Quality setting not applied");LoadPreferences();if(!lowQuality)throw new Exception("Quality setting not persisted");checks.Add("PASS options save/cancel, quality persistence");
  Size before=ClientSize;ClientSize=new Size(610,580);Options();PerformLayout();foreach(NumericUpDown number in usbBoxes)if(number.Width<65)throw new Exception("USB number control clipped");SaveWindow("options-small.png");CloseOverlay();ClientSize=before;CenterButtons();checks.Add("PASS responsive USB controls");
  About();if(!AboutText.Contains("GPL-2.0-or-later")||!AboutText.Contains("MattUan")||Application.OpenForms.Count!=1)throw new Exception("About content/forms");SaveWindow("about.png");CloseOverlay();checks.Add("PASS about and license");
  forceBackgroundForTest=true;int polls=Native.PollCalls;Tick(this,EventArgs.Empty);if(Native.PollCalls!=polls)throw new Exception("Background input was polled");forceBackgroundForTest=false;checks.Add("PASS no keyboard/gamepad polling in background");
  ImeMode old=ImeMode;using(GameImeScope scope=new GameImeScope(this,picture)){if(ImeMode!=ImeMode.Disable||picture.ImeMode!=ImeMode.Disable)throw new Exception("IME enabled during gameplay");}if(ImeMode!=old)throw new Exception("IME not restored");checks.Add("PASS IME scope and restore");
  Turn(1);if(page!=2)throw new Exception("Page+");Turn(-1);Turn(-1);if(page!=30)throw new Exception("Wrap");Turn(1);if(!status.Text.Contains("0=10"))throw new Exception("0=10 hint missing");checks.Add("PASS menu navigation");
  picture.OsdText="选择01\n请稍后\n正在下载";SaveWindow("osd.png");picture.OsdText="";SaveWindow("menu-vcd.png");picture.LowQuality=false;SaveWindow("menu-original.png");picture.LowQuality=lowQuality;
  File.WriteAllLines(Path.Combine(userDir,"smoke-result.txt"),checks.ToArray());Close();
 }
}
}
