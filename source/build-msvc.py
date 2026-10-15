from pathlib import Path
import re,subprocess,concurrent.futures,sys,time,shutil
sys.stdout.reconfigure(encoding='utf-8',errors='replace')
root=Path(__file__).parent.resolve();src=root/'core-source/fceux-fceux-2.7.0/src'
cm=(src/'CMakeLists.txt').read_text().split('set(SRC_CORE',1)[1].split('set(SRC_DRIVERS_COMMON',1)[0]
files=list(dict.fromkeys(re.findall(r'\$\{CMAKE_CURRENT_SOURCE_DIR\}/([^\s\)]+)',cm)))
files=[src/f for f in files]+[root/'headless.cpp',src/'utils/unzip.cpp',src/'utils/ioapi.cpp']
objdir=root/'obj-msvc';objdir.mkdir(exist_ok=True)
flags=['/nologo','/c','/O2','/MT','/EHsc','/std:c++14','/DMSVC=1','/D_CRT_SECURE_NO_WARNINGS','/DNOMINMAX','/DPSS_STYLE=2','/I'+str(src),'/I'+str(root/'zlib'),'/I'+str(src.parent/'vc/defaultconfig'),'/w']
def compile_one(item):
 i,f=item;out=objdir/(str(i)+'.obj')
 if out.exists() and out.stat().st_mtime>f.stat().st_mtime:return out
 p=subprocess.run(['cl',*flags,str(f),'/Fo'+str(out)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',errors='replace')
 if p.returncode:raise RuntimeError(str(f)+'\n'+p.stdout)
 return out
try:
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:objects=list(ex.map(compile_one,enumerate(files)))
 print('Compiled',len(objects),'objects',flush=True)
 if not (root/'zlib/zlib-v070.lib').exists():subprocess.run(['lib','/nologo','/def:'+str(root/'zlib/zlib1.def'),'/machine:x64','/out:'+str(root/'zlib/zlib-v070.lib')],check=True)
 rsp=root/'link.rsp';rsp.write_text('\n'.join('"'+str(p)+'"' for p in objects))
 stamp=str(time.time_ns());binary=root/('vcdcore-build-'+stamp+'.dll')
 subprocess.run(['link','/nologo','/DLL','/INCREMENTAL:NO','/IMPLIB:'+str(objdir/('exports-'+stamp+'.lib')),'/OUT:'+str(binary),'@'+str(rsp),str(root/'zlib/zlib-v070.lib'),'winmm.lib','shlwapi.lib','user32.lib','advapi32.lib','shell32.lib'],check=True)
 shutil.copyfile(binary,root/'vcdcore.dll')
 print('BUILT',root/'vcdcore.dll',flush=True)
except Exception as e:print(e,flush=True);sys.exit(1)
