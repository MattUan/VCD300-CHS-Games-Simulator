using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;

[assembly:System.Reflection.AssemblyTitle("VCD300-GAME Simulator")]
[assembly:System.Reflection.AssemblyCompany("MattUan")]
[assembly:System.Reflection.AssemblyProduct("VCD中文游戏300模拟器")]
[assembly:System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly:System.Reflection.AssemblyFileVersion("1.1.0.0")]
[assembly:System.Reflection.AssemblyInformationalVersion("1.1 beta")]

namespace VCD300 {
static class Program {
 internal const string Version="V1.1 beta";
 [STAThread] static void Main(string[] args) {
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try {
   Menu menu=new Menu();
   if(args.Contains("--display-smoke-test"))menu.Shown+=delegate{menu.DisplaySmoke();};
   if(args.Contains("--smoke-test"))menu.Shown+=delegate{menu.Smoke();};
   if(args.Contains("--integration-test"))menu.Shown+=delegate{string arg=args.FirstOrDefault(x=>x.StartsWith("--integration-game="));int n=3;if(arg!=null)int.TryParse(arg.Substring(19),out n);menu.Integration(n,args.Contains("--vcd-quality"));};
   Application.Run(menu);
  }catch(Exception ex){MessageBox.Show("程序未能启动：\n"+ex.Message,"中文游戏300");}
 }
}
static class Native {
 internal static int PollCalls;
 [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 internal static bool Down(Keys k){PollCalls++;return (GetAsyncKeyState((int)k)&0x8000)!=0;}
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
 readonly TableLayoutPanel layout=new TableLayoutPanel();
 readonly float[] savedFooterRows=new float[3];bool windowReady,changingWindow,resizePending;Rectangle normalBounds;Size normalMinimum;
 readonly string[] names={"A","B","SELECT","START","↑","↓","←","→"};
 Keys[] key1={Keys.F,Keys.D,Keys.S,Keys.Enter,Keys.Up,Keys.Down,Keys.Left,Keys.Right};
 Keys[] key2={Keys.NumPad1,Keys.NumPad3,Keys.NumPad0,Keys.Decimal,Keys.NumPad8,Keys.NumPad2,Keys.NumPad4,Keys.NumPad6};
 bool lowQuality=true,full,integration,loading,armInput=true,forceBackgroundForTest;
 int page=1,slot=1,previousPad;string runtime="";
 readonly Gamepads gamepads=new Gamepads();readonly PlayerInput[] profiles={new PlayerInput(),new PlayerInput()};
 readonly bool[] armPlayers={true,true};readonly bool[] padConnected=new bool[2];
 ComboBox[] modeBoxes,deviceBoxes;Label[] monitorLabels;Button monitorButton;bool monitoring;
 PadDevice[] availablePads=new PadDevice[0];DateTime lastMonitor=DateTime.MinValue;
 Player game;Bitmap gameBitmap;GameImeScope ime;uint displayedFrame;DateTime loadingStarted,loadingUntil,chord=DateTime.MinValue,lastInfo=DateTime.MinValue;
 CheckBox qualityBox;NumericUpDown[] usbBoxes;Button saveOptions,cancelOptions;TableLayoutPanel optionContent;
 internal Menu(){
  Text="VCD300-GAME Simulator · "+Program.Version;
  using(Stream icon=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("VCD300.AppIcon.ico")){if(icon!=null)Icon=new Icon(icon);}BackColor=Color.FromArgb(15,18,23);ForeColor=Color.White;
  Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
  ClientSize=new Size(880,792);MinimumSize=new Size(600,610);MaximizeBox=false;KeyPreview=true;
  Directory.CreateDirectory(userDir);LoadPreferences();picture.LowQuality=lowQuality;
  layout.Dock=DockStyle.Fill;layout.ColumnCount=1;layout.RowCount=4;layout.Margin=Padding.Empty;
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
  Deactivate+=delegate{StopMonitoring();ClearInput();armInput=true;if(game!=null)game.Paused=true;if(ime!=null)ime.RestoreLanguage();UpdateInfo(false);};
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
 [StructLayout(LayoutKind.Sequential)] struct WindowRect {internal int Left,Top,Right,Bottom;}
 int FooterHeight {get{int[] rows=layout.GetRowHeights();return rows.Length==4?rows[1]+rows[2]+rows[3]:132;}}
 void NormalizeWindow(){
  if(!windowReady||changingWindow||full||WindowState!=FormWindowState.Normal||IsDisposed)return;
  changingWindow=true;
  try{PerformLayout();Rectangle target=WindowGeometry.Constrain(Bounds,Bounds,2,new Size(Width-ClientSize.Width,Height-ClientSize.Height),FooterHeight,MinimumSize);if(target.Size!=Size)Size=target.Size;}
  finally{changingWindow=false;}
 }
 protected override void OnShown(EventArgs e){windowReady=true;base.OnShown(e);if(!IsDisposed)NormalizeWindow();}
 protected override void OnResize(EventArgs e){
  base.OnResize(e);if(!windowReady||changingWindow||full||resizePending||!IsHandleCreated||IsDisposed)return;
  resizePending=true;try{BeginInvoke((MethodInvoker)delegate{resizePending=false;NormalizeWindow();});}catch(InvalidOperationException){resizePending=false;}
 }
 protected override void OnResizeEnd(EventArgs e){base.OnResizeEnd(e);NormalizeWindow();}
 protected override void WndProc(ref Message m){
  if(m.Msg==0x214&&windowReady&&!full&&WindowState==FormWindowState.Normal){
   WindowRect raw=(WindowRect)Marshal.PtrToStructure(m.LParam,typeof(WindowRect));Rectangle rect=Rectangle.FromLTRB(raw.Left,raw.Top,raw.Right,raw.Bottom);
   rect=WindowGeometry.Constrain(rect,Bounds,m.WParam.ToInt32(),new Size(Width-ClientSize.Width,Height-ClientSize.Height),FooterHeight,MinimumSize);
   raw.Left=rect.Left;raw.Top=rect.Top;raw.Right=rect.Right;raw.Bottom=rect.Bottom;Marshal.StructureToPtr(raw,m.LParam,false);m.Result=(IntPtr)1;return;
  }base.WndProc(ref m);
 }
 void SetFooter(bool visible){
  layout.SuspendLayout();
  for(int i=1;i<4;i++){if(!visible)savedFooterRows[i-1]=layout.RowStyles[i].Height;layout.RowStyles[i].Height=visible?savedFooterRows[i-1]:0;}
  status.Visible=buttonHost.Visible=pads.Visible=visible;layout.ResumeLayout(true);PerformLayout();
 }
 void ToggleFull(){
  changingWindow=true;
  try{
   if(!full){normalBounds=Bounds;normalMinimum=MinimumSize;Rectangle screen=Screen.FromControl(this).Bounds;full=true;MinimumSize=Size.Empty;WindowState=FormWindowState.Normal;SetFooter(false);FormBorderStyle=FormBorderStyle.None;Bounds=screen;}
   else{full=false;FormBorderStyle=FormBorderStyle.Sizable;SetFooter(true);MinimumSize=normalMinimum;WindowState=FormWindowState.Normal;Bounds=normalBounds;}
  }finally{changingWindow=false;}
  if(!full)NormalizeWindow();if(!overlay.Visible)picture.Focus();picture.Invalidate();
 }
 internal void DisplaySmoke(){
  timer.Stop();List<string> checks=new List<string>();NormalizeWindow();
  if(Text.Contains("【")||Text.Contains("】")||!Text.Contains("V1.1 beta")||Icon==null)throw new Exception("Title or icon missing");checks.Add("PASS title without Chinese brackets and embedded application icon");
  Rectangle before=Bounds;
  for(int edge=1;edge<=8;edge++){
   WindowRect raw=new WindowRect{Left=before.Left,Top=before.Top,Right=before.Left+1027,Bottom=before.Top+701};IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WindowRect)));
   try{Marshal.StructureToPtr(raw,memory,false);Message message=Message.Create(Handle,0x214,(IntPtr)edge,memory);WndProc(ref message);raw=(WindowRect)Marshal.PtrToStructure(memory,typeof(WindowRect));int cw=raw.Right-raw.Left-(Width-ClientSize.Width),ch=raw.Bottom-raw.Top-(Height-ClientSize.Height)-FooterHeight;if(cw*3!=ch*4)throw new Exception("WM_SIZING aspect failed at edge "+edge);}
   finally{Marshal.FreeHGlobal(memory);}
  }checks.Add("PASS native resize constraints on all 8 edges and corners");
  ClientSize=new Size(1031,701);NormalizeWindow();if(picture.Width*3!=picture.Height*4)throw new Exception("Window viewport not 4:3");SaveWindow("window-resize.png");checks.Add("PASS programmatic resize normalized to 4:3 game viewport");
  Rectangle normal=Bounds;ToggleFull();PerformLayout();Rectangle target=Screen.FromControl(this).Bounds;
  if(Bounds!=target||status.Visible||pads.Visible||buttonHost.Visible||FooterHeight!=0||picture.ClientSize!=ClientSize)throw new Exception("Fullscreen footer/bounds failed");
  Rectangle canvas=WindowGeometry.Fit(picture.ClientSize);if(canvas.Width*3!=canvas.Height*4||canvas.Left<=0)throw new Exception("Fullscreen pillarbox failed");SaveWindow("fullscreen.png");checks.Add("PASS fullscreen fills display, hides all footer rows and centers 4:3 image");
  ToggleFull();if(Bounds!=normal||!status.Visible||!pads.Visible||!buttonHost.Visible)throw new Exception("Window bounds/footer not restored");checks.Add("PASS fullscreen exit restores window size and footer");
  for(int i=0;i<3;i++){ToggleFull();ToggleFull();}if(Bounds!=normal||FooterHeight<=0)throw new Exception("Repeated fullscreen toggles changed layout");checks.Add("PASS repeated fullscreen transitions");
  File.WriteAllLines(Path.Combine(userDir,"display-result.txt"),checks.ToArray());Close();
 }

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
 void ClearInput(){if(game!=null){game.P1=0;game.P2=0;}armPlayers[0]=armPlayers[1]=true;chord=DateTime.MinValue;previousPad=0;}
 void Tick(object sender,EventArgs e){
  bool foreground=Foreground;int p1=0,p2=0;
  if(overlay.Visible){if(foreground&&monitoring)MonitorPads();else if(!foreground&&monitoring)StopMonitoring();return;}
  // Do not poll either global keyboard state or gamepad state in the background.
  if(foreground){p1=ReadPad(0);p2=ReadPad(1);}
  if(game==null){
   if(foreground&&!overlay.Visible){
    if(armInput){if((p1|p2)==0)armInput=false;previousPad=p1|p2;}
    else{int menuPad=p1|p2;int edge=menuPad&~previousPad;previousPad=menuPad;if((edge&64)!=0)Turn(-1);if((edge&128)!=0)Turn(1);if((edge&16)!=0){slot=(slot+8)%10+1;}if((edge&32)!=0){slot=slot%10+1;}if((edge&9)!=0)Launch();}
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
   int a=InputRouting.Resolve(profiles[0],profiles[0].Kind==InputKind.Keyboard?Keyboard(key1):0,p1),b=InputRouting.Resolve(profiles[1],profiles[1].Kind==InputKind.Keyboard?Keyboard(key2):0,p2);
   a=GatePlayer(0,a);b=GatePlayer(1,b);game.P1=a;game.P2=b;
   if(Native.Down(Keys.Escape)){game.Stop=true;ClearInput();}
   if((profiles[0].Kind==InputKind.Gamepad&&(a&12)==12)||(profiles[1].Kind==InputKind.Gamepad&&(b&12)==12)){if(chord==DateTime.MinValue)chord=DateTime.UtcNow;if((DateTime.UtcNow-chord).TotalMilliseconds>=800){game.Stop=true;ClearInput();}}else chord=DateTime.MinValue;
  }else{ClearInput();armInput=true;}
  CoreStats stats=game.Stats;if(stats.frames!=displayedFrame){stats=game.PaintFrame(gameBitmap);displayedFrame=stats.frames;picture.FrameChanged();}
  if(integration&&stats.frames>=1050&&!File.Exists(Path.Combine(userDir,"host-game.png"))){gameBitmap.Save(Path.Combine(userDir,"embedded-game.png"));SaveWindow("host-game.png");}
  RefreshInfo(foreground);
 }
 void RefreshInfo(bool foreground){if((DateTime.UtcNow-lastInfo).TotalMilliseconds<250)return;lastInfo=DateTime.UtcNow;UpdateInfo(foreground);}
 int ReadPad(int player){if(profiles[player].Kind!=InputKind.Gamepad){padConnected[player]=false;return 0;}PadSnapshot snapshot=gamepads.Read(profiles[player].DeviceId);if(snapshot.Connected!=padConnected[player]){armPlayers[player]=true;armInput=true;previousPad=0;}padConnected[player]=snapshot.Connected;return snapshot.Map(profiles[player].Buttons);}
 int GatePlayer(int player,int bits){if(armPlayers[player]){if(bits==0)armPlayers[player]=false;return 0;}return bits;}
 string PlayerStatus(int player){PlayerInput profile=profiles[player];if(profile.Kind==InputKind.Keyboard)return (player+1)+"P：键盘";string name=gamepads.Name(profile.DeviceId).Split(new char[]{'·'})[0].Trim();return (player+1)+"P："+name+(padConnected[player]?"":"（未连接）");}
 void UpdateInfo(bool foreground){
  string quality=lowQuality?"仿 VCD 效果":"原始画质";
  SetText(pads,PlayerStatus(0)+" · "+PlayerStatus(1)+" · "+quality+(game!=null&&!foreground?" · 已暂停":""));
  if(game!=null&&!loading)SetText(status,"G"+((page-1)*10+slot).ToString("000")+" · Esc 返回菜单 · F11 全屏"+(!foreground?" · 窗口未激活":""));
 }
 void ReturnMenu(){if(game!=null){game.Stop=true;ClearInput();}CloseOverlay();if(game==null)RenderMenu();}
 void FinishGame(){ClearInput();if(game!=null){game.Dispose();game=null;}if(picture.Image==gameBitmap)picture.Image=null;if(gameBitmap!=null){gameBitmap.Dispose();gameBitmap=null;}loading=false;picture.OsdText="";if(ime!=null){ime.Dispose();ime=null;}armInput=true;}
 void LoadPreferences(){
  try{string[] lines=File.ReadAllLines(Path.Combine(userDir,"keys.txt"));if(lines.Length>=17){Keys[] one=new Keys[8],two=new Keys[8];for(int i=0;i<8;i++){one[i]=(Keys)int.Parse(lines[i]);two[i]=(Keys)int.Parse(lines[i+8]);}if(one.Distinct().Count()==8&&two.Distinct().Count()==8){key1=one;key2=two;}}}catch{}
  InputRouting.Load(userDir,profiles);
  try{lowQuality=File.ReadAllText(Path.Combine(userDir,"display.txt")).Trim()=="vcd=1";}catch{}
 }
 void SavePreferences(Keys[] one,Keys[] two,PlayerInput[] draft,bool vcd){
  List<string> lines=new List<string>();lines.AddRange(one.Select(k=>((int)k).ToString()));lines.AddRange(two.Select(k=>((int)k).ToString()));lines.Add("0");
  File.WriteAllLines(Path.Combine(userDir,"keys.txt"),lines.ToArray());InputRouting.Save(userDir,draft);File.WriteAllText(Path.Combine(userDir,"display.txt"),vcd?"vcd=1":"vcd=0");
  key1=one;key2=two;profiles[0]=draft[0].Copy();profiles[1]=draft[1].Copy();ClearInput();lowQuality=vcd;picture.LowQuality=vcd;if(game!=null)game.LowQuality=vcd;
 }
 void OpenOverlay(){StopMonitoring();ClearInput();if(game!=null)game.Paused=true;foreach(Control c in overlay.Controls.Cast<Control>().ToArray())c.Dispose();overlay.Controls.Clear();overlay.Visible=true;overlay.BringToFront();picture.Visible=false;}
 void CloseOverlay(){StopMonitoring();overlay.Visible=false;picture.Visible=true;armInput=true;previousPad=0;if(game!=null)game.Paused=loading||(!Foreground&&!integration);picture.Focus();UpdateInfo(Foreground);}
 Label TextLabel(string text,int height){Label l=new Label();l.Text=text;l.Dock=DockStyle.Fill;l.AutoSize=false;l.Height=height;l.TextAlign=ContentAlignment.MiddleLeft;l.Margin=new Padding(0,4,0,4);return l;}
 TableLayoutPanel Stack(){TableLayoutPanel t=new TableLayoutPanel();t.ColumnCount=1;t.AutoSize=true;t.AutoSizeMode=AutoSizeMode.GrowAndShrink;t.Dock=DockStyle.Top;t.Margin=Padding.Empty;t.Padding=new Padding(18,12,18,16);return t;}
 void AddRow(TableLayoutPanel t,Control c,int height){int row=t.RowCount++;t.RowStyles.Add(new RowStyle(height<0?SizeType.AutoSize:SizeType.Absolute,Math.Max(0,height)));c.Dock=DockStyle.Fill;t.Controls.Add(c,0,row);}
 void RefreshPadChoices(PlayerInput[] draft){
  string[] wanted=new string[2];for(int p=0;p<2;p++){PadDevice old=deviceBoxes[p].SelectedItem as PadDevice;wanted[p]=old==null?draft[p].DeviceId:old.Id;}
  if(Foreground)availablePads=gamepads.Enumerate();
  for(int p=0;p<2;p++){
   ComboBox box=deviceBoxes[p];box.Items.Clear();foreach(PadDevice item in availablePads)box.Items.Add(item);
   if(wanted[p].Length>0&&!availablePads.Any(x=>x.Id==wanted[p]))box.Items.Add(new PadDevice(wanted[p],gamepads.Name(wanted[p])+"（未连接）"));
   int selected=-1;for(int i=0;i<box.Items.Count;i++)if(((PadDevice)box.Items[i]).Id==wanted[p])selected=i;
   if(selected<0){string other=wanted[1-p];for(int i=0;i<box.Items.Count;i++)if(modeBoxes[1-p].SelectedIndex==0||((PadDevice)box.Items[i]).Id!=other){selected=i;break;}}
   if(box.Items.Count==0)box.Items.Add(new PadDevice("","未检测到手柄，请连接后刷新"));box.SelectedIndex=selected<0?0:selected;
  }
 }
 void StopMonitoring(){monitoring=false;if(monitorButton!=null&&!monitorButton.IsDisposed)monitorButton.Text="开始检测按键";}
 void MonitorPads(){
  if((DateTime.UtcNow-lastMonitor).TotalMilliseconds<100)return;lastMonitor=DateTime.UtcNow;
  for(int p=0;p<2;p++){
   if(modeBoxes[p].SelectedIndex==0){SetText(monitorLabels[p],(p+1)+"P：键盘模式");continue;}
   PadDevice item=deviceBoxes[p].SelectedItem as PadDevice;if(item==null||item.Id.Length==0){SetText(monitorLabels[p],(p+1)+"P：未选择手柄");continue;}
   PadSnapshot raw=gamepads.Read(item.Id);int[] map=Enumerable.Range(p*4,4).Select(i=>(int)usbBoxes[i].Value).ToArray();int bits=raw.Map(map);
   string actions=((bits&1)!=0?" A":"")+((bits&2)!=0?" B":"")+((bits&4)!=0?" SELECT":"")+((bits&8)!=0?" START":"");
   SetText(monitorLabels[p],(p+1)+"P "+raw.Pressed()+(actions.Length>0?" →"+actions:""));
  }
 }
 void Options(){
  OpenOverlay();Keys[] one=(Keys[])key1.Clone(),two=(Keys[])key2.Clone();PlayerInput[] draft={profiles[0].Copy(),profiles[1].Copy()};
  Panel actions=new Panel();actions.Dock=DockStyle.Bottom;actions.Height=54;overlay.Controls.Add(actions);
  Panel scroll=new Panel();scroll.Dock=DockStyle.Fill;scroll.AutoScroll=true;overlay.Controls.Add(scroll);scroll.BringToFront();
  optionContent=Stack();scroll.Controls.Add(optionContent);Label heading=TextLabel("【选项】",34);heading.Font=new Font(Font.FontFamily,15);AddRow(optionContent,heading,42);
  AddRow(optionContent,TextLabel("双人模式在游戏标题页由1P选择；这里仅分配控制设备。\n1P、2P可分别使用键盘或指定手柄，连接手柄不会改变分配。",48),56);
  Label message=TextLabel("",58);message.ForeColor=Color.Gold;
  TableLayoutPanel deviceGrid=new TableLayoutPanel();deviceGrid.ColumnCount=3;deviceGrid.RowCount=3;deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,18));deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,41));deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,41));
  deviceGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,28));deviceGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,38));deviceGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
  deviceGrid.Controls.Add(TextLabel("【玩家设备】",24),0,0);deviceGrid.Controls.Add(TextLabel("1P",24),1,0);deviceGrid.Controls.Add(TextLabel("2P",24),2,0);
  deviceGrid.Controls.Add(TextLabel("操作方式",30),0,1);deviceGrid.Controls.Add(TextLabel("手柄设备",30),0,2);
  modeBoxes=new ComboBox[2];deviceBoxes=new ComboBox[2];
  for(int p=0;p<2;p++){
   ComboBox mode=new ComboBox();mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Items.AddRange(new object[]{"键盘","手柄"});mode.SelectedIndex=draft[p].Kind==InputKind.Keyboard?0:1;mode.Dock=DockStyle.Fill;mode.Margin=new Padding(4,4,4,4);modeBoxes[p]=mode;deviceGrid.Controls.Add(mode,p+1,1);
   ComboBox device=new ComboBox();device.DropDownStyle=ComboBoxStyle.DropDownList;device.DropDownWidth=480;device.Dock=DockStyle.Fill;device.Margin=new Padding(4,4,4,4);device.Enabled=mode.SelectedIndex==1;deviceBoxes[p]=device;deviceGrid.Controls.Add(device,p+1,2);
  }
  for(int p=0;p<2;p++){int player=p;modeBoxes[p].SelectedIndexChanged+=delegate{deviceBoxes[player].Enabled=modeBoxes[player].SelectedIndex==1;};}
  AddRow(optionContent,deviceGrid,110);RefreshPadChoices(draft);
  FlowLayoutPanel tools=new FlowLayoutPanel();tools.WrapContents=false;Button refresh=Btn("刷新手柄",delegate{RefreshPadChoices(draft);});refresh.Width=120;tools.Controls.Add(refresh);
  monitorButton=Btn("开始检测按键",delegate{monitoring=!monitoring;monitorButton.Text=monitoring?"停止检测按键":"开始检测按键";lastMonitor=DateTime.MinValue;if(monitoring&&Foreground)MonitorPads();});monitorButton.Width=150;tools.Controls.Add(monitorButton);AddRow(optionContent,tools,42);
  monitorLabels=new Label[]{TextLabel("1P：点击检测后按手柄按钮",28),TextLabel("2P：点击检测后按手柄按钮",28)};foreach(Label label in monitorLabels)AddRow(optionContent,label,32);
  AddRow(optionContent,TextLabel("【手柄按键】USB编号对应 joy.cpl；每位玩家分别设置。\nXInput：1=A、2=B、3=X、4=Y、5=LB、6=RB；\n7=BACK、8=START、9=L3、10=R3、11=LT、12=RT。",70),84);
  TableLayoutPanel padGrid=new TableLayoutPanel();padGrid.ColumnCount=5;padGrid.RowCount=3;padGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,16));for(int i=0;i<4;i++)padGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,21));
  padGrid.Controls.Add(TextLabel("玩家",26),0,0);for(int i=0;i<4;i++)padGrid.Controls.Add(TextLabel(names[i],26),i+1,0);usbBoxes=new NumericUpDown[8];
  padGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
  for(int p=0;p<2;p++){padGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,36));padGrid.Controls.Add(TextLabel((p+1)+"P",28),0,p+1);for(int i=0;i<4;i++){NumericUpDown number=new NumericUpDown();number.Minimum=1;number.Maximum=32;number.Value=draft[p].Buttons[i];number.Dock=DockStyle.Fill;number.Margin=new Padding(6,3,6,3);number.TextAlign=HorizontalAlignment.Center;usbBoxes[p*4+i]=number;padGrid.Controls.Add(number,i+1,p+1);}}
  AddRow(optionContent,padGrid,106);
  AddRow(optionContent,TextLabel("同一实体手柄可能同时显示为USB与XInput，请勿作为两只手柄分配。\n按 joy.cpl 编号设置时请选择USB条目；游戏内按住SELECT+START约1秒返回。\n部分游戏不处理2P的START/SELECT；标题页的双人选择请使用1P。",72),84);
  AddRow(optionContent,TextLabel("【键盘按键】点击下方按钮，再按新按键；Esc、F11保留。",28),34);
  TableLayoutPanel keys=new TableLayoutPanel();keys.ColumnCount=3;keys.RowCount=9;keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));keys.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39));
  keys.Controls.Add(TextLabel("操作说明",24),0,0);keys.Controls.Add(TextLabel("1P 键盘",24),1,0);keys.Controls.Add(TextLabel("2P 键盘",24),2,0);keys.RowStyles.Add(new RowStyle(SizeType.Absolute,30));Button listening=null;
  for(int i=0;i<8;i++){int index=i;keys.RowStyles.Add(new RowStyle(SizeType.Absolute,38));keys.Controls.Add(TextLabel(names[i],32),0,i+1);
   for(int p=0;p<2;p++){Keys[] map=p==0?one:two;Button b=Btn(map[i].ToString(),delegate{});b.Dock=DockStyle.Fill;b.ImeMode=ImeMode.Disable;b.Margin=new Padding(4,3,4,3);keys.Controls.Add(b,p+1,i+1);
    b.Click+=delegate{if(listening!=null)listening.Text="请重新选择";listening=b;b.Text="请按下按键";b.Focus();};
    b.KeyDown+=delegate(object o,KeyEventArgs ev){if(listening!=b)return;ev.Handled=true;ev.SuppressKeyPress=true;if(ev.KeyCode==Keys.Escape||ev.KeyCode==Keys.F11){message.Text="该键由程序保留，请使用其他按键。";return;}map[index]=ev.KeyCode;b.Text=ev.KeyCode.ToString();listening=null;message.Text="";};
   }
  }AddRow(optionContent,keys,342);
  qualityBox=new CheckBox();qualityBox.Text="仿 VCD 效果（画面柔化、降低音质，默认开启）";qualityBox.Checked=lowQuality;qualityBox.AutoSize=true;AddRow(optionContent,qualityBox,40);AddRow(optionContent,message,64);
  FlowLayoutPanel row=new FlowLayoutPanel();row.AutoSize=true;row.WrapContents=false;actions.Controls.Add(row);
  saveOptions=Btn("保存并返回",delegate{
   if(one.Distinct().Count()!=8||two.Distinct().Count()!=8){message.Text="同一玩家的八个键位不能重复。";scroll.ScrollControlIntoView(message);return;}
   for(int p=0;p<2;p++){
    PadDevice device=deviceBoxes[p].SelectedItem as PadDevice;draft[p].Kind=modeBoxes[p].SelectedIndex==0?InputKind.Keyboard:InputKind.Gamepad;draft[p].DeviceId=device==null?"":device.Id;draft[p].Buttons=Enumerable.Range(p*4,4).Select(i=>(int)usbBoxes[i].Value).ToArray();
    if(!InputRouting.ValidMap(draft[p].Buttons)){message.Text=(p+1)+"P 的四个手柄按钮编号不能重复。";scroll.ScrollControlIntoView(message);return;}
    if(draft[p].Kind==InputKind.Gamepad&&!Gamepads.ValidId(draft[p].DeviceId)){message.Text="请为 "+(p+1)+"P 选择手柄，连接后可点击刷新。";scroll.ScrollControlIntoView(message);return;}
    if(draft[p].Kind==InputKind.Gamepad&&draft[p].DeviceId.StartsWith("xinput:")&&draft[p].Buttons.Any(x=>x>12)){message.Text="XInput使用1–12的标准编号；joy.cpl编号请选择USB条目。";scroll.ScrollControlIntoView(message);return;}
   }
   if(draft.All(x=>x.Kind==InputKind.Gamepad)&&draft[0].DeviceId==draft[1].DeviceId){message.Text="1P和2P不能选择同一个手柄条目。";scroll.ScrollControlIntoView(message);return;}
   try{SavePreferences(one,two,draft,qualityBox.Checked);CloseOverlay();}catch(Exception ex){message.Text="保存失败："+ex.Message;}
  });saveOptions.Width=132;cancelOptions=Btn("取消",delegate{CloseOverlay();});cancelOptions.Width=100;row.Controls.Add(saveOptions);row.Controls.Add(cancelOptions);
  EventHandler center=delegate{row.Location=new Point(Math.Max(0,(actions.ClientSize.Width-row.Width)/2),8);};actions.Resize+=center;row.SizeChanged+=center;center(null,EventArgs.Empty);
 }

 const string AboutText="【VCD300-GAME Simulator · V1.1 beta】\r\nVCD中文游戏300模拟器\r\n\r\n【开发者】\r\n开发者：MattUan\r\nGitHub：https://github.com/MattUan\r\n\r\n【游戏来源】\r\n本程序全部游戏 ROM 与原菜单提取自《中文游戏300》VCD光盘。300个菜单选项对应150款不同游戏。\r\n\r\n【开源与许可】\r\n模拟器内核基于 FCEUX 2.7.0，版权归 FCEUX 团队及其贡献者所有，遵循 GNU GPL v2 或更新版本（GPL-2.0-or-later）。本程序亦按 GPL-2.0-or-later 提供源码。完整许可和第三方版权声明见 licenses，源代码见 source。代码已公开：https://github.com/MattUan/VCD300-CHS-Games-Simulator 。\r\n开源软件许可不代表游戏 ROM 与原盘素材的再分发授权。\r\n\r\n【操作说明】\r\n1–9 选本页游戏，0=10（本页第10项）；+ / − 翻页；方向键移动；F=A，D=B，S=SELECT，Enter=START；Esc 返回菜单；F11 全屏/窗口；全屏隐藏底栏、保持4:3画面。\r\n双人模式需在支持双人的游戏标题页，由1P按 SELECT 选择2人，再按 START；部分游戏使用方向键选择。主菜单不切换单双人。\r\n仿 VCD 效果默认开启，菜单强度为游戏画面的50%，同时降低游戏音质；可在【选项】中关闭。\r\n\r\n【版本测试】\r\n当前版本为测试版，欢迎大家积极参与测试，如遇bug欢迎在github中提交Issues。";
 void About(){OpenOverlay();Panel footer=new Panel();footer.Dock=DockStyle.Bottom;footer.Height=52;overlay.Controls.Add(footer);Button back=Btn("返回",delegate{CloseOverlay();});footer.Controls.Add(back);EventHandler center=delegate{back.Location=new Point(Math.Max(0,(footer.ClientSize.Width-back.Width)/2),8);};footer.Resize+=center;footer.Layout+=delegate{center(null,EventArgs.Empty);};center(null,EventArgs.Empty);Panel body=new Panel();body.Dock=DockStyle.Fill;body.Padding=new Padding(22,18,22,6);overlay.Controls.Add(body);body.BringToFront();TextBox text=new TextBox();text.Multiline=true;text.ReadOnly=true;text.ScrollBars=ScrollBars.Vertical;text.BorderStyle=BorderStyle.None;text.BackColor=BackColor;text.ForeColor=ForeColor;text.Font=Font;text.Dock=DockStyle.Fill;text.Text=AboutText;body.Controls.Add(text);}
 void SaveWindow(string name){PerformLayout();overlay.PerformLayout();Update();using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(Path.Combine(userDir,name));}}
 internal void Integration(int n,bool vcd){if(n<1||n>300)n=3;integration=true;lowQuality=vcd;picture.LowQuality=vcd;if(game!=null)game.LowQuality=vcd;page=(n-1)/10+1;slot=(n-1)%10+1;RenderMenu();Launch();}
 void InputSmoke(List<string> checks){
  PlayerInput[] original={profiles[0].Copy(),profiles[1].Copy()};
  Options();modeBoxes[0].SelectedIndex=0;modeBoxes[1].SelectedIndex=1;deviceBoxes[1].Items.Clear();deviceBoxes[1].Items.Add(new PadDevice("winmm:63","USB测试设备（未连接）"));deviceBoxes[1].SelectedIndex=0;usbBoxes[6].Value=8;usbBoxes[7].Value=7;
  if(deviceBoxes[0].Enabled||!deviceBoxes[1].Enabled)throw new Exception("Player mode controls not independent");SaveWindow("options-mixed.png");saveOptions.PerformClick();
  if(overlay.Visible||profiles[0].Kind!=InputKind.Keyboard||profiles[1].Kind!=InputKind.Gamepad||profiles[1].DeviceId!="winmm:63"||profiles[1].Buttons[2]!=8||profiles[1].Buttons[3]!=7)throw new Exception("Keyboard+pad assignment save failed");
  InputRouting.Load(userDir,profiles);if(profiles[1].DeviceId!="winmm:63")throw new Exception("Player device not persisted");checks.Add("PASS 1P keyboard + 2P selected single pad, per-player buttons and persistence");
  Options();modeBoxes[0].SelectedIndex=1;modeBoxes[1].SelectedIndex=0;deviceBoxes[0].Items.Clear();deviceBoxes[0].Items.Add(new PadDevice("winmm:63","USB测试设备（未连接）"));deviceBoxes[0].SelectedIndex=0;saveOptions.PerformClick();
  if(profiles[0].Kind!=InputKind.Gamepad||profiles[1].Kind!=InputKind.Keyboard)throw new Exception("Pad+keyboard assignment save failed");checks.Add("PASS 1P selected pad + 2P keyboard");
  Options();modeBoxes[0].SelectedIndex=0;modeBoxes[1].SelectedIndex=1;cancelOptions.PerformClick();if(profiles[0].Kind!=InputKind.Gamepad||profiles[1].Kind!=InputKind.Keyboard)throw new Exception("Cancel changed device assignment");
  Options();modeBoxes[1].SelectedIndex=1;deviceBoxes[1].Items.Clear();deviceBoxes[1].Items.Add(new PadDevice("winmm:63","USB测试设备（未连接）"));deviceBoxes[1].SelectedIndex=0;string saved=File.ReadAllText(Path.Combine(userDir,"input.txt"));saveOptions.PerformClick();if(!overlay.Visible||saved!=File.ReadAllText(Path.Combine(userDir,"input.txt")))throw new Exception("Duplicate device accepted");cancelOptions.PerformClick();checks.Add("PASS assignment cancel and duplicate device rejection");
  Options();monitoring=true;forceBackgroundForTest=true;int before=Native.PollCalls;Tick(this,EventArgs.Empty);forceBackgroundForTest=false;if(monitoring||Native.PollCalls!=before)throw new Exception("Monitor polled in background");CloseOverlay();checks.Add("PASS explicit pad detection stops on focus loss");
  armPlayers[0]=armPlayers[1]=true;if(GatePlayer(0,128)!=0||GatePlayer(1,0)!=0||GatePlayer(1,1)!=1)throw new Exception("One player's held pad blocked the other player");checks.Add("PASS independent release gates for both players");
  profiles[0]=original[0];profiles[1]=original[1];InputRouting.Save(userDir,profiles);ClearInput();
 }
 internal void Smoke(){
  timer.Stop();List<string> checks=new List<string>();PerformLayout();CenterButtons();if(Math.Abs(buttons.Left*2+buttons.Width-buttonHost.ClientSize.Width)>2)throw new Exception("Buttons not centered");checks.Add("PASS centered footer");
  InputSmoke(checks);
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
