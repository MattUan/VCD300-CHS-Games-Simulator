"""Extract VCD payloads using MIPS address getters, never a guessed marker.
Input: original 2352-byte-sector disc image. Output: ROMs + layout evidence.
Only the four layouts proven against this disc are accepted.
"""
from pathlib import Path
import struct,hashlib,json,csv,collections,sys
BASE=0x4001a400
OPCODES={0xef:0x30,0xf2:0xd0,0xf3:0x90,0xf4:0xf0,0xf7:0x4c,0xfa:0xf0,0xfb:0xd0,0xfc:0xf0,0xff:0x4c}
def getter(b,o):
 a,j,c=struct.unpack_from('>III',b,o)
 if a&0xffff0000!=0x3c020000 or j!=0x03e00008 or c&0xffff0000!=0x24420000:return None
 return ((a&65535)<<16)+struct.unpack_from('>h',b,o+10)[0]
def layout(b):
 groups=[]
 for o in range(0,min(len(b),0x12000)-48,4):
  g=[getter(b,o+i*12) for i in range(4)]
  if g[0] and g[0]==g[1]==g[2] and g[3] and 0<g[0]-BASE<g[3]-BASE<=len(b):groups.append((o,g))
 if len(groups)!=1:raise ValueError('Ambiguous/missing MIPS range getters')
 o,g=groups[0];start,end=g[0]-BASE,g[3]-BASE;length=end-start
 # The constructor immediately before the four getters writes mirror mode
 # into gp+0x1c7. The PPU handler proves 0=clear A11 (vertical),
 # 1=clear A10 (horizontal). Track constants rather than offset guesses.
 regs=[None]*32;regs[0]=0;mirror=None;proof=None
 for k in range(max(0,o-0x240),o,4):
  w=struct.unpack_from('>I',b,k)[0];op=w>>26;rs=w>>21&31;rt=w>>16&31;rd=w>>11&31;im=w&65535;si=im if im<32768 else im-65536
  if op==15:regs[rt]=im<<16
  elif op==9:regs[rt]=(regs[rs]+si)&0xffffffff if regs[rs] is not None else None
  elif op==13:regs[rt]=regs[rs]|im if regs[rs] is not None else None
  elif op==0 and w&63 in [0x21,0x25]:regs[rd]=((regs[rs]+regs[rt]) if w&63==0x21 else (regs[rs]|regs[rt])) if regs[rs] is not None and regs[rt] is not None else None
  elif op in [32,33,35,36,37]:regs[rt]=None
  elif op==40 and rs==28 and im==0x1c7:mirror=regs[rt];proof=k
  regs[0]=0
 if mirror not in [0,1]:raise ValueError('Unresolved mirror constructor')
 sizes={0xa000:(0,32768,8192),0xc000:(3,32768,16384),0x10000:(3,32768,32768),0x20000:(2,131072,0)}
 if length not in sizes:raise ValueError('Unsupported payload length')
 mapper,prg,chr=sizes[length]
 target=BASE+o+24;call=0x0c000000|((target&0x0fffffff)>>2)
 calls=[k for k in range(0,o,4) if struct.unpack_from('>I',b,k)[0]==call]
 if len(calls)<2:raise ValueError('Missing program initialization calls')
 init=calls[1]+8
 expected_init=bytes.fromhex('3c03000100431021' if mapper==2 else '24438000af830120')
 if b[init:init+8]!=expected_init:raise ValueError('Unverified program-bank layout')
 # Verify this interpreter's custom opcode dispatch matches the reverse
 # engineered branch/jump handlers before enabling fetch translation.
 if b[0x550:0x554]!=bytes.fromhex('3c024002'):raise ValueError('Unknown dispatch initializer')
 table_offset=0x40020000+struct.unpack_from('>h',b,0x556)[0]-BASE
 table=struct.unpack_from('>256I',b,table_offset)
 expected={0xef:0x32a4,0xf2:0x3364,0xf3:0x3378,0xf4:0x338c,0xf7:0x3438,0xfa:0x34c8,0xfb:0x34dc,0xfc:0x34f0,0xff:0x35a0}
 if table_offset==0x35ac:
  expected={x:(addr-8 if x>=0xfa else addr) for x,addr in expected.items()}
 elif table_offset!=0x35b4:raise ValueError('Unknown interpreter version')
 if any(table[x]-BASE!=addr for x,addr in expected.items()):raise ValueError('Unknown opcode interpreter')
 # The reset routine reads the last bank for UxROM, first 32K otherwise.
 vector_at=start+prg-6;vectors=struct.unpack_from('<HHH',b,vector_at)
 if vectors[1]<0x8000:raise ValueError('Invalid reset vector in recovered program')
 return dict(payload_start=start,payload_start_hex=hex(start),payload_end=end,payload_end_hex=hex(end),payload_length=length,chr_start_hex=hex(start+prg) if chr else 'CHR-RAM',getter_offset=o,program_bank_proof=init,mirror_store=proof,vcd_mirror=mirror,nes_mirror=1-mirror,mapper=mapper,prg_bytes=prg,chr_bytes=chr,nmi=vectors[0],reset=vectors[1],irq=vectors[2],dispatch_table=table_offset)
def main(iso,out):
 out=Path(out);(out/'roms').mkdir(parents=True,exist_ok=True);(out/'tests').mkdir(exist_ok=True)
 raw=Path(iso).read_bytes()
 if len(raw)%2352:raise ValueError('Expected raw 2352-byte-sector image')
 data=b''.join(raw[i+24:i+2072] for i in range(0,len(raw),2352))
 if data[16*2048+1:16*2048+6]!=b'CD001':raise ValueError('Invalid disc filesystem')
 files=[]
 def walk(ext,size,parent=''):
  buf=data[ext*2048:ext*2048+size];o=0
  while o<len(buf):
   if not buf[o]:o=(o//2048+1)*2048;continue
   r=buf[o:o+buf[o]];o+=len(r);name=r[33:33+r[32]].decode('ascii');e=int.from_bytes(r[2:6],'little');s=int.from_bytes(r[10:14],'little')
   if name in ['\0','\1']:continue
   path=parent+'/'+name
   if r[25]&2:walk(e,s,path)
   else:files.append((name,path,e,s))
 r=data[16*2048+156:16*2048+190];walk(int.from_bytes(r[2:6],'little'),int.from_bytes(r[10:14],'little'))
 rows=[];cached={}
 for name,path,ext,size in files:
  if not (path.startswith('/NEWROM/G') and name.endswith('.BIN;1')):continue
  n=int(name[1:4]);b=data[ext*2048:ext*2048+size];sha=hashlib.sha256(b).hexdigest()
  if sha not in cached:
   params=layout(b);mapper=params['mapper'];h=b'NES\x1a'+bytes([params['prg_bytes']//16384,params['chr_bytes']//8192,(mapper<<4)|params['nes_mirror'],0])+bytes(8)
   nes=h+b[params['payload_start']:params['payload_end']];cached[sha]=(n,params,nes)
  master,params,nes=cached[sha];(out/'roms'/f'G{n:03}.nes').write_bytes(nes)
  rows.append(dict(game=n,representative=master,iso_path=path,sector=ext,bin_size=size,bin_sha256=sha,**params,nes_sha256=hashlib.sha256(nes).hexdigest()))
 rows.sort(key=lambda r:r['game'])
 if len(rows)!=300 or len(cached)!=150:raise ValueError('Unexpected disc inventory')
 (out/'tests/disc-layout.json').write_text(json.dumps(dict(iso_sha256=hashlib.sha256(raw).hexdigest(),method='MIPS getters and constructor constants; payload bytes unchanged',games=rows),indent=2),encoding='utf-8')
 with (out/'tests/disc-layout.csv').open('w',encoding='utf-8-sig',newline='') as f:
  w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
 print('Recovered',len(rows),'entries;',len(cached),'unique; mappers',dict(collections.Counter(r['mapper'] for r in rows)),flush=True)
if __name__=='__main__':main(sys.argv[1],sys.argv[2])
