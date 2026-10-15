from pathlib import Path
import re,subprocess,concurrent.futures,sys
root=Path(__file__).parent.resolve();src=root/'core-source/fceux-fceux-2.7.0/src'
# Two lightweight observations are shared by both target builds.
for file,old,new in [('input.cpp','static DECLFR(JPRead)\n{','static DECLFR(JPRead)\n{\n extern void VCDReadPad();VCDReadPad();'),('sound.cpp','static DECLFW(Write_PSG)\n{','static DECLFW(Write_PSG)\n{\n extern void VCDWriteApu();VCDWriteApu();')]:
 p=src/file;s=p.read_text();
 if new not in s:assert old in s;p.write_text(s.replace(old,new,1))
cm=(src/'CMakeLists.txt').read_text().split('set(SRC_CORE',1)[1].split('set(SRC_DRIVERS_COMMON',1)[0]
files=list(dict.fromkeys(re.findall(r'\$\{CMAKE_CURRENT_SOURCE_DIR\}/([^\s\)]+)',cm)))
files=[str(src/f) for f in files]
files.append(str(root/'headless.cpp'))
files += [str(src/'utils/unzip.cpp'),str(src/'utils/ioapi.cpp')]
target=sys.argv[1] if len(sys.argv)>1 else 'linux'
compiler='g++' if target=='linux' else 'x86_64-w64-mingw32-g++'
objdir=root/('obj-'+target);objdir.mkdir(exist_ok=True)
flags=['-O2','-std=c++14','-fPIC','-DHAVE_ASPRINTF','-I'+str(src),'-Wno-write-strings','-Wno-narrowing']
if target=='windows':flags+=['-DPSS_STYLE=2','-I'+str(root/'zlib-cross')]
def compile_one(item):
 i,f=item;out=objdir/(str(i)+'.o')
 if out.exists() and out.stat().st_mtime>Path(f).stat().st_mtime:return out
 p=subprocess.run([compiler,*flags,'-c',f,'-o',str(out)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 if p.returncode:raise RuntimeError(f+'\n'+p.stdout)
 return out
try:
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:objects=list(ex.map(compile_one,enumerate(files)))
 output=root/('libvcdcore.so' if target=='linux' else 'vcdcore.dll')
 cmd=[compiler,'-shared','-o',str(output),*map(str,objects)]
 if target=='linux':cmd+=['-lz','-lpthread','-Wl,--no-undefined']
 else:cmd+=['-static-libgcc','-static-libstdc++','-L'+str(root/'zlib-cross'),'-lz','-lwinmm','-lshlwapi','-lpthread']
 subprocess.run(cmd,check=True)
 print('BUILT',output,len(objects),'objects')
except Exception as e:print(e);sys.exit(1)
