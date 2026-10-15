#include <windows.h>
#include <cstdio>
#include <cstdint>
#include <string>
struct Stats{uint32_t frames,pc,reads,apu,jammed,samples,signalFrames,peak,hash;};
int wmain(int argc,wchar_t**argv){
 if(argc<3)return 2;
 HMODULE dll=LoadLibraryA("vcdcore.dll");if(!dll){printf("LOAD DLL FAILED %lu\n",GetLastError());return 3;}
 auto load=(int(*)(const char*,const char*))GetProcAddress(dll,"vcd_load");
 auto step=(int(*)(int,int))GetProcAddress(dll,"vcd_step");
 auto stats=(void(*)(Stats*))GetProcAddress(dll,"vcd_stats");
 auto video=(void*(*)())GetProcAddress(dll,"vcd_video");
 auto close=(void(*)())GetProcAddress(dll,"vcd_close");
 int size=WideCharToMultiByte(CP_UTF8,0,argv[1],-1,0,0,0,0);std::string rom(size,'\0');WideCharToMultiByte(CP_UTF8,0,argv[1],-1,&rom[0],size,0,0);
 if(!load(rom.c_str(),".")){puts("LOAD ROM FAILED");return 4;}
 bool baseline=argc>3&&wcscmp(argv[3],L"baseline")==0;
 for(int i=0;i<1800;++i){int a=0;if(!baseline){if(i>=600&&i<630)a=8;else if(i>=900){int p=(i-900)%600;a=p<150?129:p<300?66:p<450?16:32;}}step(a,0);}
 Stats s;stats(&s);printf("{\"frames\":%u,\"pc\":%u,\"reads\":%u,\"apu\":%u,\"jammed\":%u,\"samples\":%u,\"signalFrames\":%u,\"peak\":%u,\"hash\":%u}\n",s.frames,s.pc,s.reads,s.apu,s.jammed,s.samples,s.signalFrames,s.peak,s.hash);
 FILE*f=_wfopen(argv[2],L"wb");if(!f)return 5;fwrite(video(),1,245760,f);fclose(f);close();return 0;
}
