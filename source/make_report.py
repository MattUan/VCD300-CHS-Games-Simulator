from pathlib import Path
import json,html,collections,sys
root=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parent.parent;tests=root/'tests'
obj=json.loads((tests/'results.json').read_text('utf-8'));rows=obj['games'];counts=collections.Counter(r['status'] for r in rows)
labels={'basic':'基础指标通过（画面待确认）','review':'需重点复查','fault':'检测到运行故障'}
reason={'no screenshot difference after input':'未检出输入后截图差异','no controller reads':'未检出手柄读取','no varying audio signal':'未检出变化音频信号','last snapshot mostly one color':'末帧大部分为同色（可能停留在选关或转场）','native error or CPU jam':'原生错误或CPU停滞'}
sections=[]
for r in rows:
 a=r['input'];b=r['baseline'];n=r['game'];title=labels[r['status']];why='；'.join(reason.get(x,x) for x in r['reason'].split('; ') if x)
 imgs=[]
 for mode,x in [('无输入',b),('有输入',a)]:
  imgs.append('<div><b>'+mode+'</b><div class="shots">'+''.join('<a href="'+s['file']+'"><img loading="lazy" src="'+s['file']+'" alt="G'+str(n)+' '+str(s['frame'])+'帧"><span>'+str(s['frame'])+'帧</span></a>' for s in x.get('snapshots',[]))+'</div></div>')
 sections.append('<article data-status="'+r['status']+'" data-game="'+f'{n:03}'+'"><h2>G'+f'{n:03}'+' · '+title+'</h2><p>共用结果：G'+f'{r["representative"]:03}'+'；Mapper '+str(r['mapper'])+'；'+('纵向镜像' if r['mirror'] else '横向镜像')+'</p><p>'+html.escape(why or '未触发基础异常标记，仍需人工检查画面、声音和完整流程。')+'</p><p>运行帧 '+str(a.get('frames','—'))+' · 手柄读取 '+str(a.get('reads','—'))+' · 音频信号帧 '+str(a.get('signalFrames','—'))+' · 原盘特殊指令 '+str(a.get('customOpcodes','—'))+' · 图形换页 '+str(a.get('chrWrites','—'))+'</p>'+''.join(imgs)+'</article>')
page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>中文游戏300 V0.70 beta 测试</title><style>body{font:16px system-ui,sans-serif;background:#11151b;color:#e9eef5;margin:24px auto;max-width:1100px;padding:0 16px}h1{font-size:26px}h2{font-size:20px}p{line-height:1.7}a{color:#a8d4ff}article{padding:18px;background:#1c232e;border-radius:10px;margin:20px 0}input,select{padding:10px;font:inherit;margin:8px}.shots{display:flex;gap:10px;margin:12px 0;flex-wrap:wrap}.shots a{width:256px}.shots img{display:block;width:256px;image-rendering:pixelated}.shots span{font-size:13px}header{position:sticky;top:0;background:#11151bf5;padding:8px;z-index:2}.note{background:#302a1d;padding:16px;border-radius:10px}</style><h1>中文游戏300 · V0.70 beta 自动测试</h1><p>Windows x64 原生 DLL；150 款不同游戏各执行无输入、有输入两组测试，每组 1800 帧。</p><p class="note">全部300项已按原盘MIPS加载代码重新转换。基础指标通过不代表游戏已完整可玩；本测试没有原机逐帧参照，也没有人工听音或通关。输入为第600–629帧START，第900帧后轮流方向与A/B；选关、二次确认等界面可能需要更多操作。</p><p>SUMMARY</p><p><a href="disc-layout.csv">原盘位置与参数表</a> · <a href="results.csv">测试CSV</a> · <a href="results.json">原始指标JSON</a></p><header><input id="q" placeholder="搜索编号，例如003"><select id="status"><option value="">全部状态</option><option value="basic">基础指标通过</option><option value="review">需重点复查</option><option value="fault">运行故障</option></select></header>SECTIONS<script>function filter(){let q=document.getElementById('q').value.replace(/G/ig,'').trim(),s=document.getElementById('status').value;document.querySelectorAll('article').forEach(a=>a.hidden=!(a.dataset.game.includes(q)&&(!s||a.dataset.status===s)))}document.getElementById('q').oninput=filter;document.getElementById('status').onchange=filter;</script></html>'''
page=page.replace('SUMMARY','；'.join(labels[k]+' '+str(counts[k])+' 项' for k in ['basic','review','fault'])).replace('SECTIONS',''.join(sections))
(tests/'report.html').write_text(page,encoding='utf-8')
masters=[r for r in rows if r['game']==r['representative']]
(tests/'contact-index.tsv').write_text('\n'.join(f"{r['game']}\t{r['status'].upper()}" for r in masters),encoding='ascii')
print(dict(counts))
