using System;using System.IO;using System.Linq;using System.Runtime.InteropServices;
namespace VCD300 {
static class InputTests {
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static PlayerInput Pad(string id,int[] map){return new PlayerInput{Kind=InputKind.Gamepad,DeviceId=id,Buttons=map};}
 static void Main(string[] args){
  string dir=args[0];Directory.CreateDirectory(dir);Check(Marshal.SizeOf(typeof(Gamepads.JoyCaps))==728&&Marshal.SizeOf(typeof(Gamepads.JoyInfo))==52,"Windows joystick struct sizes");
  Gamepads.JoyCaps caps=new Gamepads.JoyCaps{Xmin=0,Xmax=255,Ymin=0,Ymax=255,NumButtons=8};
  Gamepads.JoyInfo state=new Gamepads.JoyInfo{X=128,Y=128,Pov=0};
  PadSnapshot snapshot=Gamepads.FromWinMM(state,caps);Check(snapshot.Directions==0,"no phantom direction for absent POV or 0..255 axes");
  state.Buttons=1u<<6;snapshot=Gamepads.FromWinMM(state,caps);Check(snapshot.Map(new[]{1,2,7,8})==4,"USB physical button 7 -> SELECT");Check(snapshot.Map(new[]{1,2,8,7})==8,"USB physical button 7 -> START after remap");
  state.Buttons=1u<<7;snapshot=Gamepads.FromWinMM(state,caps);Check(snapshot.Map(new[]{1,2,7,8})==8,"USB physical button 8 -> START");Check(snapshot.Map(new[]{1,2,8,7})==4,"USB physical button 8 -> SELECT after remap");
  state.Buttons=1u<<31;snapshot=Gamepads.FromWinMM(state,caps);Check(snapshot.Map(new[]{32,2,7,8})==1,"USB button 32 bit handling");
  state.Buttons=0;state.X=0;state.Y=255;Check(Gamepads.FromWinMM(state,caps).Directions==(64|32),"driver-reported axis scaling");
  state.X=128;state.Y=128;state.Pov=4500;caps.Caps=0x10;Check(Gamepads.FromWinMM(state,caps).Directions==(16|128),"POV diagonal when supported");
  state.Pov=0xffff;Check(Gamepads.FromWinMM(state,caps).Directions==0,"centered POV");
  snapshot=Gamepads.FromXInput(new Gamepads.XState{Buttons=0x20});Check(snapshot.Map(new[]{1,2,7,8})==4&&snapshot.Map(new[]{1,2,8,7})==8,"XInput uses the configured SELECT/START map");
  snapshot=Gamepads.FromXInput(new Gamepads.XState{Buttons=0x10});Check(snapshot.Map(new[]{1,2,7,8})==8,"XInput START logical button 8");
  PlayerInput keyboard=new PlayerInput(),pad=Pad("winmm:4",new[]{1,2,8,7});
  Check(InputRouting.Resolve(keyboard,129,8)==129&&InputRouting.Resolve(pad,2,4)==4,"keyboard + single pad assigned to 2P");
  Check(InputRouting.Resolve(pad,129,8)==8&&InputRouting.Resolve(keyboard,2,4)==2,"single pad assigned to 1P + keyboard");
  Check(InputRouting.Resolve(pad,255,0)==0&&InputRouting.Resolve(keyboard,1,0)==1,"unplugged pad does not steal keyboard or other player");
  Check(new PadSnapshot().Map(new[]{1,2,7,8})==0,"disconnected device clears pad input");
  Check(!InputRouting.ValidMap(new[]{1,2,7,7})&&!InputRouting.ValidMap(new[]{1,2,0,33}),"invalid button mappings rejected");
  Check(Gamepads.ValidId("winmm:4")&&Gamepads.ValidId("xinput:3")&&!Gamepads.ValidId("xinput:4")&&!Gamepads.ValidId("winmm:-1"),"explicit device IDs and slot gaps");
  File.WriteAllLines(Path.Combine(dir,"pad.txt"),new[]{"1","2","8","7"});PlayerInput[] loaded={new PlayerInput(),new PlayerInput()};InputRouting.Load(dir,loaded);Check(loaded.All(p=>p.Kind==InputKind.Keyboard&&p.Buttons.SequenceEqual(new[]{1,2,8,7})),"legacy mapping migration does not auto-assign pads");
  InputRouting.Save(dir,new[]{keyboard,pad});InputRouting.Load(dir,loaded);Check(loaded[0].Kind==InputKind.Keyboard&&loaded[1].Kind==InputKind.Gamepad&&loaded[1].DeviceId=="winmm:4"&&loaded[1].Buttons.SequenceEqual(pad.Buttons),"independent player mode/device/button persistence");
  InputRouting.Save(dir,new[]{pad,keyboard});InputRouting.Load(dir,loaded);Check(loaded[0].Kind==InputKind.Gamepad&&loaded[1].Kind==InputKind.Keyboard,"reverse device assignment persists");
  InputRouting.Save(dir,new[]{pad,pad});loaded=new[]{new PlayerInput(),new PlayerInput()};InputRouting.Load(dir,loaded);Check(loaded.All(p=>p.Kind==InputKind.Keyboard),"duplicate device configuration rejected");
  Console.WriteLine("ALL "+count+" input checks passed");
 }
}
}
