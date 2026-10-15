from pathlib import Path
import ctypes as C,json,sys,hashlib,subprocess,concurrent.futures,csv,time
from collections import Counter
ROOT=Path(__file__).parent.resolve();OUT=ROOT/'auto-tests';OUT.mkdir(exist_ok=True)
FIELDS=['frames','pc','reads','apu','jammed','samples','signalFrames','peak','hash']
class Stats(C.Structure):_fields_=[(x,C.c_uint32) for x in FIELDS]
def worker(rom,case,dest):
 from PIL import Image
 lib=C.CDLL(str(ROOT/'libvcdcore.so'));lib.vcd_load.argtypes=[C.c_char_p,C.c_char_p];lib.vcd_video.restype=C.c_void_p;lib.vcd_error.restype=C.c_char_p
 target=Path(dest);target.mkdir(parents=True,exist_ok=True)
 for d in ['sav','fcs','cheats']: (target/d).mkdir(exist_ok=True)
 if lib.vcd_load(str(Path(rom).resolve()).encode(),str(target).encode())!=1:
  return {'error':(lib.vcd_error() or b'load failure').decode(errors='replace')}
 shots=[];hashes=[];pcs=[]
 for frame in range(1800):
  a=0
  if case=='input':
   if 600<=frame<630:a=8
   elif frame>=900:
    phase=(frame-900)%600
    a=129 if phase<150 else (66 if phase<300 else (16 if phase<450 else 32))
  lib.vcd_step(a,0)
  if frame%30==0:
   st=Stats();lib.vcd_stats(C.byref(st));hashes.append(st.hash);pcs.append(st.pc)
  if frame in [590,950,1790]:
   data=C.string_at(lib.vcd_video(),245760);im=Image.frombytes('RGBA',(256,240),data,'raw','BGRA').convert('RGB')
   colors=im.getcolors(61440);dominant=max(n for n,c in colors)/61440
   shots.append({'frame':frame+1,'hash':hashlib.sha256(data).hexdigest(),'colors':len(colors),'dominant':round(dominant,5)})
   if case=='input':im.save(target/(str(frame+1)+'.png'))
 st=Stats();lib.vcd_stats(C.byref(st));result={f:getattr(st,f) for f in FIELDS}
 result.update(snapshots=shots,frame_variants=len(set(hashes)),pc_variants=len(set(pcs)),error=(lib.vcd_error() or b'').decode(errors='replace'))
 lib.vcd_close();return result
def run_pair(item):
 digest,rom=item;folder=OUT/digest[:12];folder.mkdir(exist_ok=True)
 result={}
 for case in ['baseline','input']:
  cache=folder/(case+'.json')
  if cache.exists():result[case]=json.loads(cache.read_text());continue
  try:
   p=subprocess.run([sys.executable,str(Path(__file__).resolve()),'--worker',str(rom),case,str(folder/case)],capture_output=True,text=True,timeout=25)
   if p.returncode:res={'error':'worker exit '+str(p.returncode),'detail':p.stderr[-1000:]}
   else:res=json.loads(p.stdout)
  except subprocess.TimeoutExpired:res={'error':'timeout (25s)'}
  cache.write_text(json.dumps(res,ensure_ascii=False,indent=2));result[case]=res
 return digest,result
def main():
 romdir=ROOT.parent/'roms'
 if not romdir.exists():romdir=ROOT.parent/'outputs/VCD300_v0.5.0/roms'
 groups={};numhash={}
 for rom in sorted(romdir.glob('*.nes')):
  h=hashlib.sha256(rom.read_bytes()).hexdigest();groups.setdefault(h,rom);numhash[int(rom.stem[1:])]=h
 pairs={};start=time.time()
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
  for i,(digest,res) in enumerate(ex.map(run_pair,groups.items())):
   pairs[digest]=res
   if i%10==0:print('tested',i+1,'/',len(groups),'elapsed',round(time.time()-start),flush=True)
 rows=[]
 for n in range(1,301):
  if n not in numhash:rows.append({'game':n,'status':'未恢复','reason':'无转换ROM'});continue
  digest=numhash[n];res=pairs[digest];b=res['baseline'];r=res['input'];flags=[]
  if r.get('error'):flags.append('运行错误')
  if r.get('jammed'):flags.append('CPU停滞')
  if not r.get('reads'):flags.append('未读取控制器')
  if not r.get('signalFrames'):flags.append('未检出非静音波形')
  if r.get('snapshots') and r['snapshots'][-1]['dominant']>.97:flags.append('末帧疑似空白画面（可能处于转场，需复查）')
  response=any(a['hash']!=z['hash'] for a,z in zip(r.get('snapshots',[]),b.get('snapshots',[])))
  if not response:flags.append('未检出输入后的画面差异')
  representative=int(groups[digest].stem[1:])
  if representative in [4,11,14,19,20,23,65,66,79,94,99,113,118,128,129,139,143]:flags.append('画面抽查疑似图形或换页异常')
  hard=r.get('error') or r.get('jammed')
  status='检测到运行故障' if hard else ('需重点复查' if flags else '基础指标通过（画面待确认）')
  rows.append({'game':n,'status':status,'reason':'；'.join(flags),'input_response':response,'sha256':digest,'representative':groups[digest].stem,'telemetry':r,'baseline':b})
 (OUT/'results.json').write_text(json.dumps({'method':'1800帧无输入 + 1800帧脚本输入；按ROM SHA256去重；同组结果映射到所有编号','unique_roms':len(groups),'entries':rows},ensure_ascii=False,indent=2))
 with (OUT/'results.csv').open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.writer(f);w.writerow(['游戏编号','分类','发现','输入后画面变化','实测同ROM代表','帧数','控制器读取','APU写入','非静音帧','CPU停滞'])
  for r in rows:
   t=r.get('telemetry',{});w.writerow([f'G{r["game"]:03}',r['status'],r['reason'],r.get('input_response',''),r.get('representative',''),t.get('frames',''),t.get('reads',''),t.get('apu',''),t.get('signalFrames',''),t.get('jammed','')])
 print(Counter(r['status'] for r in rows),flush=True)
if __name__=='__main__':
 if len(sys.argv)>1 and sys.argv[1]=='--worker':print(json.dumps(worker(*sys.argv[2:]),ensure_ascii=False))
 else:main()
