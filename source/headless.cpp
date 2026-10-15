// VCD300 host interface for FCEUX 2.7.0. GPL-2.0-or-later.
#include "driver.h"
#include "fceu.h"
#include "x6502.h"
#include "emufile.h"
#include "palette.h"
#include "video.h"
#include "ppu.h"
#include "cart.h"
#include "ines.h"
#include <algorithm>
#include <chrono>
#include <string>
#include <cstring>
#ifdef _WIN32
#include <windows.h>
#define API extern "C" __declspec(dllexport)
#else
#define API extern "C" __attribute__((visibility("default")))
#endif
struct Stats {uint32 frames,pc,reads,apu,jammed,samples,signalFrames,peak,hash;};
static Stats stats={};
static uint32 palette[256],video[256*240],joy=0;
static int16 audio[4096];static int audioCount=0;
static std::string error;
static bool initialized=false,loaded=false;
uint32 VCDCustomOpcodes=0;
static uint32 VCDRegisters=0;
static int vcdMapper=0;
extern uint8 SPRAM[0x100];
extern int scanline;
static readfunc originalStatus=0;
// This disc's G122 has an injected wait at $FFE0 that polls bit 6 even
// when its title-screen sprite has no opaque background overlap. The
// VCD interpreter emits that phase signal unconditionally. Scope this
// compatibility signal to the exact disc program and the injected wait.
static DECLFR(VCDStatus){
 uint8 value=originalStatus?originalStatus(A):0;
 if(X.PC>=0xffe0&&X.PC<=0xffe7&&scanline>=113&&scanline<240)value|=0x40;
 return value;
}
static DECLFW(VCDChrBank) {
 // $6802 is the bank port implemented by the original VCD interpreter.
 // Direct CHR mapping has no cartridge bus conflicts.
 FCEUPPU_LineUpdate();setchr8(V);++VCDRegisters;
}
API void vcd_diagnostics(uint32*dest){if(dest){dest[0]=VCDCustomOpcodes;dest[1]=VCDRegisters;}}
int dendy=0,pal_emulation=0,eoptions=0,KillFCEUXonFrame=0,closeFinishedMovie=0;
bool swapDuty=false,paldeemphswap=false,turbo=false;
unsigned int*GetKeyboard(){static unsigned int keys[256]={};return keys;}
void GetMouseData(uint32 (&data)[3]){data[0]=data[1]=data[2]=0;}
uint32 ModernDeemphColorMap(const uint8*src,const uint8*base,int scale){
 int offset=(int)(src-base);int x=(offset&255)/scale,y=(offset>>8)/scale;
 uint8 emph=XDBuf?XDBuf[x+y*256]:0;
 if(emph&&palo){pal p=palo[(emph&7)*64+(*src&63)];return 0xff000000u|(p.r<<16)|(p.g<<8)|p.b;}
 return palette[*src];
}
void VCDReadPad(){++stats.reads;}
void VCDWriteApu(){++stats.apu;}
void FCEUD_SetPalette(uint8 i,uint8 r,uint8 g,uint8 b){palette[i]=0xff000000|(r<<16)|(g<<8)|b;}
void FCEUD_GetPalette(uint8 i,uint8*r,uint8*g,uint8*b){*r=palette[i]>>16;*g=palette[i]>>8;*b=palette[i];}
void FCEUD_PrintError(const char*s){error=s;}
void FCEUD_Message(const char*){}
const char*FCEUD_GetCompilerString(){return "VCD300 FCEUX core (no GUI, no Lua)";}
FILE*FCEUD_UTF8fopen(const char*fn,const char*mode){
#ifdef _WIN32
 wchar_t path[32768],m[20];MultiByteToWideChar(CP_UTF8,0,fn,-1,path,32768);MultiByteToWideChar(CP_UTF8,0,mode,-1,m,20);return _wfopen(path,m);
#else
 return fopen(fn,mode);
#endif
}
EMUFILE_FILE*FCEUD_UTF8_fstream(const char*n,const char*m){return new EMUFILE_FILE(n,m);}
FCEUFILE*FCEUD_OpenArchiveIndex(ArchiveScanRecord&,std::string&,int){return 0;}
FCEUFILE*FCEUD_OpenArchiveIndex(ArchiveScanRecord&,std::string&,int,int*){return 0;}
FCEUFILE*FCEUD_OpenArchive(ArchiveScanRecord&,std::string&,std::string*){return 0;}
FCEUFILE*FCEUD_OpenArchive(ArchiveScanRecord&,std::string&,std::string*,int*){return 0;}
ArchiveScanRecord FCEUD_ScanArchive(std::string){return ArchiveScanRecord();}
int FCEUD_SendData(void*,uint32){return 0;}int FCEUD_RecvData(void*,uint32){return 0;}
void FCEUD_NetplayText(uint8*){}void FCEUD_NetworkClose(){}
void FCEUD_UpdateInput(){}
void RefreshThrottleFPS(){}
void FCEUD_VideoChanged(){}
bool FCEUD_ShouldDrawInputAids(){return false;}
void FCEUD_OnCloseGame(){}
void FCEUD_AviRecordTo(){}void FCEUD_AviStop(){}
void FCEUI_AviVideoUpdate(const unsigned char*){}void FCEUI_AviSoundUpdate(void*,int){}
bool FCEUI_AviIsRecording(){return false;}
bool FCEUI_AviEnableHUDrecording(){return false;}
bool FCEUI_AviDisableMovieMessages(){return true;}
void FCEUI_SetAviEnableHUDrecording(bool){}void FCEUI_SetAviDisableMovieMessages(bool){}
void FCEUI_UseInputPreset(int){}
bool FCEUD_PauseAfterPlayback(){return false;}
void FCEUD_SetEmulationSpeed(int){}void FCEUD_TurboOn(){}void FCEUD_TurboOff(){}void FCEUD_TurboToggle(){}
int FCEUD_ShowStatusIcon(){return 0;}void FCEUD_ToggleStatusIcon(){}void FCEUD_HideMenuToggle(){}
void FCEUD_CmdOpen(){}void FCEUD_MovieReplayFrom(){}
void FCEUD_LoadStateFrom(){}void FCEUD_SaveStateAs(){}void FCEUD_MovieRecordTo(){}
void FCEUD_SoundToggle(){}void FCEUD_SoundVolumeAdjust(int){}
void FCEUD_SetInput(bool,bool,ESI p0,ESI p1,ESIFC exp){FCEUI_SetInput(0,p0,&joy,0);FCEUI_SetInput(1,p1,&joy,0);FCEUI_SetInputFC(exp,0,0);}
void FCEUD_DebugBreakpoint(int){}void FCEUD_FlushTrace(){}void FCEUD_TraceInstruction(uint8*,int){}
void FCEUD_UpdateNTView(int,bool){}void FCEUD_UpdatePPUView(int,int){}
uint64 FCEUD_GetTime(){return std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now().time_since_epoch()).count();}
uint64 FCEUD_GetTimeFreq(){return 1000000;}
API int vcd_load(const char*rom,const char*dir){
 error.clear();if(!initialized){if(!FCEUI_Initialize())return 0;initialized=true;}
 if(loaded){FCEUI_CloseGame();loaded=false;}
 FCEUI_SetBaseDirectory(dir);FCEUI_SetVidSystem(0);FCEUI_Sound(44100);FCEUI_SetSoundVolume(100);
 FCEUI_SetSoundQuality(1);FCEUI_DisableSpriteLimitation(0);
 memset(&stats,0,sizeof(stats));memset(video,0,sizeof(video));joy=0;audioCount=0;VCDCustomOpcodes=VCDRegisters=0;
 vcdMapper=0;FILE*file=FCEUD_UTF8fopen(rom,"rb");if(file){unsigned char h[8]={};if(fread(h,1,8,file)==8)vcdMapper=(h[6]>>4)|(h[7]&0xf0);fclose(file);}
 if(!FCEUI_LoadGame(rom,1,true)){if(error.empty())error="ROM could not be loaded";return 0;}
 loaded=true;if(vcdMapper==3){SetWriteHandler(0x6802,0x6802,VCDChrBank);SetWriteHandler(0x8000,0xffff,VCDChrBank);}
 const uint8 waitCode[]={0xad,0x02,0x20,0x29,0x40,0xf0,0xf9,0x60};
 if(vcdMapper==3&&ROM_size>=2&&!memcmp(ROM+4,"Masahiro",8)&&!memcmp(ROM+0x7fe0,waitCode,sizeof(waitCode))){originalStatus=GetReadHandler(0x2002);SetReadHandler(0x2002,0x2002,VCDStatus);}
 FCEUI_SetInput(0,SI_GAMEPAD,&joy,0);FCEUI_SetInput(1,SI_GAMEPAD,&joy,0);FCEUI_SetInputFC(SIFC_NONE,0,0);return 1;
}
API int vcd_step(int p1,int p2){
 if(!loaded)return 0;joy=(uint32)(p1&255)|((uint32)(p2&255)<<8);
 uint8*screen=0;int32*sound=0;int32 count=0;FCEUI_Emulate(&screen,&sound,&count,0);
 uint32 hash=2166136261u;
 if(screen)for(int i=0;i<256*240;i++){video[i]=ModernDeemphColorMap(screen+i,screen,1);hash=(hash^video[i])*16777619u;}
 audioCount=std::min((int)count,4096);uint32 peak=0;int minimum=32767,maximum=-32768;
 for(int i=0;i<audioCount;i++){int v=sound?sound[i]:0;v=std::max(-32768,std::min(32767,v));audio[i]=(int16)v;peak=std::max(peak,(uint32)(v<0?-v:v));minimum=std::min(minimum,v);maximum=std::max(maximum,v);}
 stats.frames++;stats.pc=X.PC;stats.jammed=X.jammed;stats.samples+=audioCount;stats.hash=hash;
 stats.peak=std::max(stats.peak,peak);if(maximum-minimum>32)stats.signalFrames++;
 return 1;
}
API const void*vcd_video(){return video;}
API int vcd_audio(int16*dest,int capacity){int n=std::min(capacity,audioCount);if(dest&&n>0)memcpy(dest,audio,n*sizeof(int16));return n;}
API void vcd_stats(Stats*dest){if(dest)*dest=stats;}
API const char*vcd_error(){return error.c_str();}
API void vcd_reset(){if(loaded)FCEUI_ResetNES();}
API void vcd_close(){if(loaded){FCEUI_CloseGame();loaded=false;}}
