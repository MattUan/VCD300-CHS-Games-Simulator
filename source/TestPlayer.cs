using System;
using System.IO;
using System.Threading;
namespace VCD300 {static class TestPlayer {
 static void Wait(Player p,uint frame){DateTime until=DateTime.UtcNow.AddSeconds(10);while(p.Stats.frames<frame&&!p.Done&&DateTime.UtcNow<until)Thread.Sleep(10);if(p.Done||p.Stats.frames<frame)throw new Exception("No progress: "+p.Error);}
 static void Main(string[] args){
  string data=Path.GetFullPath(args[1]);Directory.CreateDirectory(data);
  using(Player p=new Player(Path.GetFullPath(args[0]),data,false)){
   Wait(p,60);p.Paused=true;Thread.Sleep(50);uint before=p.Stats.frames;Thread.Sleep(200);if(p.Stats.frames!=before)throw new Exception("Pause advanced frames");
   p.Paused=false;Wait(p,before+10);p.Stop=true;
  }
  using(Player p=new Player(Path.GetFullPath(args[0]),data,false)){Wait(p,30);p.Stop=true;}
  Console.WriteLine("PASS: worker pause, resume, stop and reload; no additional form or process.");
 }
}}
