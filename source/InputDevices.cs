using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VCD300 {
enum InputKind { Keyboard, Gamepad }
sealed class PlayerInput {
 internal InputKind Kind=InputKind.Keyboard;
 internal string DeviceId="";
 internal int[] Buttons={1,2,7,8}; // A, B, SELECT, START; one-based physical buttons.
 internal PlayerInput Copy(){return new PlayerInput{Kind=Kind,DeviceId=DeviceId,Buttons=(int[])Buttons.Clone()};}
}
sealed class PadDevice {
 internal readonly string Id,Name;
 internal PadDevice(string id,string name){Id=id;Name=name;}
 public override string ToString(){return Name;}
}
struct PadSnapshot {
 internal bool Connected;
 internal uint Buttons;
 internal int Directions;
 internal int Map(int[] map){if(!Connected)return 0;int bits=Directions;for(int i=0;i<4;i++)if(map[i]>=1&&map[i]<=32&&(Buttons&(1u<<(map[i]-1)))!=0)bits|=1<<i;return bits;}
 internal string Pressed(){if(!Connected)return "未连接";List<string> ids=new List<string>();for(int i=0;i<32;i++)if((Buttons&(1u<<i))!=0)ids.Add((i+1).ToString());string buttons=ids.Count==0?"无":string.Join(", ",ids.ToArray());string direction=Directions==0?"":"；方向："+((Directions&16)!=0?"↑":"")+((Directions&32)!=0?"↓":"")+((Directions&64)!=0?"←":"")+((Directions&128)!=0?"→":"");return "按键："+buttons+direction;}
}
static class InputRouting {
 internal static int Resolve(PlayerInput profile,int keyboard,int pad){return profile.Kind==InputKind.Keyboard?keyboard:pad;}
 internal static void Load(string directory,PlayerInput[] profiles){
  // Migrate old button numbers, but never infer player ownership from hot-plugging.
  try{int[] old=File.ReadAllLines(Path.Combine(directory,"pad.txt")).Select(int.Parse).ToArray();if(ValidMap(old))foreach(PlayerInput p in profiles)p.Buttons=(int[])old.Clone();}catch{}
  try{
   string[] lines=File.ReadAllLines(Path.Combine(directory,"input.txt"));if(lines.Length!=13||lines[0]!="v2")return;
   PlayerInput[] parsed=new PlayerInput[2];
   for(int p=0;p<2;p++){int offset=1+p*6;InputKind kind;if(!Enum.TryParse<InputKind>(lines[offset],out kind)||!Enum.IsDefined(typeof(InputKind),kind))return;string id=lines[offset+1];if(id.Length>0&&!Gamepads.ValidId(id))return;int[] map=new int[4];for(int i=0;i<4;i++)map[i]=int.Parse(lines[offset+2+i]);if(!ValidMap(map))return;parsed[p]=new PlayerInput{Kind=kind,DeviceId=id,Buttons=map};}
   if(parsed[0].Kind==InputKind.Gamepad&&parsed[1].Kind==InputKind.Gamepad&&parsed[0].DeviceId==parsed[1].DeviceId)return;
   for(int p=0;p<2;p++)profiles[p]=parsed[p];
  }catch{}
 }
 internal static void Save(string directory,PlayerInput[] profiles){
  List<string> lines=new List<string>{"v2"};foreach(PlayerInput p in profiles){lines.Add(p.Kind.ToString());lines.Add(p.DeviceId);lines.AddRange(p.Buttons.Select(x=>x.ToString()));}
  File.WriteAllLines(Path.Combine(directory,"input.txt"),lines.ToArray());
 }
 internal static bool ValidMap(int[] values){return values.Length==4&&values.All(x=>x>=1&&x<=32)&&values.Distinct().Count()==4;}
}
sealed class Gamepads {
 [StructLayout(LayoutKind.Sequential)] internal struct JoyInfo {public uint Size,Flags,X,Y,Z,R,U,V,Buttons,Number,Pov,Reserved1,Reserved2;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct JoyCaps {
  public ushort Mid,Pid;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;
  public uint Xmin,Xmax,Ymin,Ymax,Zmin,Zmax,NumButtons,PeriodMin,PeriodMax,Rmin,Rmax,Umin,Umax,Vmin,Vmax,Caps,MaxAxes,NumAxes,MaxButtons;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string RegistryKey;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string OemDriver;
 }
 [StructLayout(LayoutKind.Sequential)] internal struct XState {public uint Packet;public ushort Buttons;public byte LeftTrigger,RightTrigger;public short LX,LY,RX,RY;}
 [DllImport("winmm.dll")]static extern uint joyGetNumDevs();
 [DllImport("winmm.dll",CharSet=CharSet.Unicode,EntryPoint="joyGetDevCapsW")]static extern uint joyGetDevCaps(UIntPtr id,ref JoyCaps caps,uint size);
 [DllImport("winmm.dll")]static extern uint joyGetPosEx(uint id,ref JoyInfo state);
 [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")]static extern uint XI14(uint index,out XState state);
 [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")]static extern uint XI9(uint index,out XState state);
 readonly Dictionary<string,JoyCaps> capsCache=new Dictionary<string,JoyCaps>();
 readonly Dictionary<string,string> names=new Dictionary<string,string>();
 internal static bool ValidId(string id){if(id==null)return false;string[] parts=id.Split(new char[]{':'});int index;return parts.Length==2&&int.TryParse(parts[1],out index)&&index>=0&&((parts[0]=="winmm"&&index<64)||(parts[0]=="xinput"&&index<4));}
 static bool XInput(uint index,out XState state){try{return XI14(index,out state)==0;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}try{return XI9(index,out state)==0;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}state=new XState();return false;}
 internal PadDevice[] Enumerate(){
  Native.PollCalls++;List<PadDevice> result=new List<PadDevice>();capsCache.Clear();
  uint count=Math.Min(joyGetNumDevs(),64u);
  for(uint i=0;i<count;i++){
   JoyCaps caps=new JoyCaps();JoyInfo info=new JoyInfo();info.Size=(uint)Marshal.SizeOf(typeof(JoyInfo));info.Flags=0xff;
   if(joyGetDevCaps(new UIntPtr(i),ref caps,(uint)Marshal.SizeOf(typeof(JoyCaps)))!=0||joyGetPosEx(i,ref info)!=0)continue;
   string id="winmm:"+i,name="USB #"+(i+1)+" · "+caps.Name;capsCache[id]=caps;names[id]=name;result.Add(new PadDevice(id,name));
  }
  for(uint i=0;i<4;i++){XState state;if(!XInput(i,out state))continue;string id="xinput:"+i,name="XInput #"+(i+1)+" · Xbox标准映射";names[id]=name;result.Add(new PadDevice(id,name));}
  return result.ToArray();
 }
 internal string Name(string id){string value;if(names.TryGetValue(id,out value))return value;if(!ValidId(id))return "未选择手柄";string[] p=id.Split(new char[]{':'});return (p[0]=="winmm"?"USB #":"XInput #")+(int.Parse(p[1])+1);}
 internal PadSnapshot Read(string id){
  Native.PollCalls++;if(!ValidId(id))return new PadSnapshot();string[] parts=id.Split(new char[]{':'});uint index=uint.Parse(parts[1]);
  if(parts[0]=="xinput"){XState state;return XInput(index,out state)?FromXInput(state):new PadSnapshot();}
  JoyCaps caps;
  if(!capsCache.TryGetValue(id,out caps)){
   caps=new JoyCaps();if(joyGetDevCaps(new UIntPtr(index),ref caps,(uint)Marshal.SizeOf(typeof(JoyCaps)))!=0)return new PadSnapshot();capsCache[id]=caps;
  }
  JoyInfo info=new JoyInfo();info.Size=(uint)Marshal.SizeOf(typeof(JoyInfo));info.Flags=0xff;
  if(joyGetPosEx(index,ref info)!=0){capsCache.Remove(id);return new PadSnapshot();}
  return FromWinMM(info,caps);
 }
 internal static PadSnapshot FromWinMM(JoyInfo info,JoyCaps caps){
  int directions=0;double x=Axis(info.X,caps.Xmin,caps.Xmax),y=Axis(info.Y,caps.Ymin,caps.Ymax);
  if(x<-.5)directions|=64;if(x>.5)directions|=128;if(y<-.5)directions|=16;if(y>.5)directions|=32;
  // A missing POV can return zero. Only use the value when the driver reports HASPOV.
  if((caps.Caps&0x10)!=0&&info.Pov!=0xffff&&info.Pov!=0xffffffff&&info.Pov<36000){uint pov=info.Pov;if(pov>=31500||pov<=4500)directions|=16;if(pov>=4500&&pov<=13500)directions|=128;if(pov>=13500&&pov<=22500)directions|=32;if(pov>=22500&&pov<=31500)directions|=64;}
  return new PadSnapshot{Connected=true,Buttons=info.Buttons,Directions=directions};
 }
 static double Axis(uint value,uint minimum,uint maximum){return maximum>minimum?Math.Max(-1,Math.Min(1,2.0*((double)value-minimum)/(maximum-minimum)-1)):0;}
 internal static PadSnapshot FromXInput(XState state){
  uint buttons=0;ushort[] masks={0x1000,0x2000,0x4000,0x8000,0x100,0x200,0x20,0x10,0x40,0x80};
  for(int i=0;i<masks.Length;i++)if((state.Buttons&masks[i])!=0)buttons|=1u<<i;
  if(state.LeftTrigger>128)buttons|=1u<<10;if(state.RightTrigger>128)buttons|=1u<<11;
  int direction=0;if((state.Buttons&1)!=0||state.LY>16000)direction|=16;if((state.Buttons&2)!=0||state.LY< -16000)direction|=32;if((state.Buttons&4)!=0||state.LX< -16000)direction|=64;if((state.Buttons&8)!=0||state.LX>16000)direction|=128;
  return new PadSnapshot{Connected=true,Buttons=buttons,Directions=direction};
 }
}
}
