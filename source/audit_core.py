"""Windows native core paired boot/input regression; stdlib only."""
from pathlib import Path
import ctypes as C,json,sys,os,subprocess,concurrent.futures,collections,csv,struct,zlib,time,hashlib
NAMES='frames pc reads apu jammed samples signalFrames peak hash'.split()
class Stats(C.Structure):_fields_=[(n,C.c_uint32) for n in NAMES]
def png(path,raw):
 rgba=bytearray(len(raw));rgba[0::4]=raw[2::4];rgba[1::4]=raw[1::4];rgba[2::4]=raw[0::4];rgba[3::4]=b'\xff'*(256*240)
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 data=b''.join(b'\0'+rgba[y*1024:(y+1)*1024] for y in range(240))
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',256,240,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(data,6))+chunk(b'IEND',b''))
def worker(root,n,mode):
 root=Path(root).resolve();(root/'tests/screens').mkdir(parents=True,exist_ok=True);os.add_dll_directory(str(root));dll=C.CDLL(str(root/'vcdcore.dll'))
 dll.vcd_load.argtypes=[C.c_char_p,C.c_char_p];dll.vcd_step.argtypes=[C.c_int,C.c_int];dll.vcd_stats.argtypes=[C.POINTER(Stats)];dll.vcd_video.restype=C.c_void_p;dll.vcd_error.restype=C.c_char_p;dll.vcd_diagnostics.argtypes=[C.POINTER(C.c_uint32)]
 data=root/'tests/runtime'/f'G{n:03}-{mode}';data.mkdir(parents=True,exist_ok=True)
 rom=root/'roms'/f'G{n:03}.nes'
 if not dll.vcd_load(str(rom).encode('utf-8'),str(data).encode('utf-8')):raise RuntimeError(str(dll.vcd_error()))
 s=Stats();diag=(C.c_uint32*2)();snapshots=[];hashes=set();pcs=set()
 try:
  for frame in range(1800):
   p=0
   if mode=='input':
    if 600<=frame<630:p=8
    elif frame>=900:p=[129,66,16,32][((frame-900)%600)//150]
   dll.vcd_step(p,0)
   if frame%30==0 or frame in [590,950,1790]:
    dll.vcd_stats(C.byref(s));hashes.add(s.hash);pcs.add(s.pc)
    if s.jammed:break
   if frame in [590,950,1790]:
    raw=C.string_at(dll.vcd_video(),256*240*4);name=f'G{n:03}-{mode}-{frame+1}.png';png(root/'tests/screens'/name,raw)
    count=collections.Counter(struct.unpack('<61440I',raw));snapshots.append(dict(file='screens/'+name,frame=frame+1,hash=s.hash,dominant=max(count.values())/61440,colors=len(count)))
  dll.vcd_stats(C.byref(s));dll.vcd_diagnostics(diag)
  result={k:getattr(s,k) for k in NAMES};result.update(customOpcodes=diag[0],chrWrites=diag[1],hashVariants=len(hashes),pcVariants=len(pcs),snapshots=snapshots)
  return result
 finally:dll.vcd_close()
def main(root):
 root=Path(root).resolve();tests=root/'tests';(tests/'screens').mkdir(parents=True,exist_ok=True)
 layout=json.loads((tests/'disc-layout.json').read_text())['games'];masters=sorted(set(r['representative'] for r in layout));jobs=[(n,m) for n in masters for m in ['baseline','input']];results={};start=time.time()
 def run(job):
  n,mode=job
  try:
   p=subprocess.run([sys.executable,__file__,'--worker',str(root),str(n),mode],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=40,creationflags=subprocess.CREATE_NO_WINDOW)
   if p.returncode:return n,mode,dict(error=p.stderr.strip() or p.stdout.strip() or 'Native crash '+str(p.returncode))
   return n,mode,json.loads(p.stdout)
  except Exception as e:return n,mode,dict(error=str(e))
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
  for i,(n,mode,res) in enumerate(ex.map(run,jobs)):
   results.setdefault(n,{})[mode]=res
   if i%20==19:print('Completed',i+1,'/',len(jobs),'elapsed',round(time.time()-start),'s',flush=True)
   (tests/'checkpoint.json').write_text(json.dumps(results),encoding='utf-8')
 rows=[]
 for l in layout:
  pair=results[l['representative']];b=pair['baseline'];a=pair['input'];reason=[]
  if any(x.get('error') or x.get('jammed') for x in [b,a]):status='fault';reason.append('native error or CPU jam')
  else:
   response=any(x['hash']!=y['hash'] for x,y in zip(a['snapshots'],b['snapshots']))
   if not response:reason.append('no screenshot difference after input')
   if not a['reads']:reason.append('no controller reads')
   if not a['signalFrames']:reason.append('no varying audio signal')
   if a['snapshots'][-1]['dominant']>.97:reason.append('last snapshot mostly one color')
   status='review' if reason else 'basic'
  rows.append(dict(game=l['game'],representative=l['representative'],status=status,reason='; '.join(reason),mapper=l['mapper'],mirror=l['nes_mirror'],baseline=b,input=a))
 (tests/'results.json').write_text(json.dumps(dict(method='Windows x64 native DLL; paired 1800 frames; same input schedule as v0.5; three PNG snapshots; no semantic playability certification',core_sha256=hashlib.sha256((root/'vcdcore.dll').read_bytes()).hexdigest(),games=rows),ensure_ascii=False,indent=2),encoding='utf-8')
 with (tests/'results.csv').open('w',encoding='utf-8-sig',newline='') as f:
  fields=['game','representative','status','reason','mapper','mirror',*NAMES,'customOpcodes','chrWrites'];w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
  for r in rows:w.writerow({k:(r[k] if k in r else r['input'].get(k,'')) for k in fields})
 (tests/'compatibility.tsv').write_text('\n'.join(str(r['game'])+'\t'+{'basic':'基础指标通过（画面待确认）','review':'需重点复查','fault':'检测到运行故障'}[r['status']] for r in rows),encoding='utf-8')
 print('RESULT',dict(collections.Counter(r['status'] for r in rows)),'seconds',round(time.time()-start),flush=True)
if __name__=='__main__':
 sys.stdout.reconfigure(encoding='utf-8')
 if sys.argv[1]=='--worker':print(json.dumps(worker(sys.argv[2],int(sys.argv[3]),sys.argv[4])))
 else:main(sys.argv[1])
