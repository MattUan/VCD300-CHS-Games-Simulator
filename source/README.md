# V1.0 beta 源码与复现

本版应用和核心DLL均在Windows x64编译、运行验证。未使用GitHub Actions。

## 应用

执行 `Build-App.ps1`，使用Windows自带Framework64 C#编译器，生成上一级目录的VCD300.exe。无NuGet依赖。

`VCD300.cs`负责菜单、内嵌设置/说明、键盘和手柄；`NativePlayer.cs`负责核心调用、后台运行、画面同步、WaveOut音频。

## 原生核心

准备Python 3、Visual Studio 2026 C++ x64工具集和Windows SDK。执行本目录的 `Fetch-Core.ps1`，获取固定提交的 FCEUX 2.7.0 并应用 `core-overrides` 中的8个修改文件，使 `core-source/fceux-fceux-2.7.0/src` 存在。提交及修改文件校验值见 `core-overrides.json`。

在VS x64开发者命令行执行：

```
python build-msvc.py
```

生成本目录vcdcore.dll，复制到应用根目录。首次构建应使用空的obj-msvc目录；修改头文件或编译选项后请重新生成对象文件。构建不生成PDB，避免此前环境中的PDB组件冲突。使用/MT静态链接C/C++运行时，运行依赖zlib1.dll；无需额外安装VC运行库。

zlib目录含对应头文件和zlib1.def，构建时生成导入库。`headless.cpp`只暴露核心接口，不加载FCEUX GUI或Lua。`REVERSE-ENGINEERING.md`说明原盘参数、九种指令及$6802端口。

`build_core.py`保留GCC/MinGW构建入口；本版正式Windows二进制来自MSVC构建。

## 从原盘转换

```
python convert_disc.py "原始镜像.iso" "输出目录"
```

脚本仅接受本盘已验证的2352字节原始扇区布局，验证MIPS地址函数、程序映射、镜像常量、指令分发表和复位向量，输出300个NES文件与disc-layout.json/csv。原盘载荷字节不修改。本版全部ROM由此脚本重新生成。

## 自动检查

```
python audit_core.py "应用根目录"
python make_report.py "应用根目录"
```

仅使用Python标准库。Windows原生DLL实际运行；按150款不同ROM去重，每款无输入/有输入各1800帧，四个工作进程，单项40秒超时。采集591、951、1791帧PNG、CPU停滞、输入读取、音频信号、特殊指令和换页次数。运行数据写入tests/runtime，仅用于开发检查。

`tools/Run-AutoTests.ps1`及core-check.exe提供不安装Python的简化复测，只比较最终帧及基础指标，标记规则不完全相同。

主程序支持 `--smoke-test` 和 `--integration-test --integration-game=2`；运行目录由环境变量VCD300_DATA_DIR指定，用于隔离测试。测试结果不等于完整游玩或声音正确性验证。

## 许可

完整修改后的FCEUX源码、接口源码及构建脚本随包提供，遵循GPL-2.0-or-later。第三方许可证见licenses。source目录的旧版auto_test.py是先前Linux复测脚本，当前Windows验证使用audit_core.py。

## Beta 界面与G122兼容

VCD300.cs增加输入法作用域、焦点门控、响应式选项、加载OSD、关于信息。NativePlayer.cs内GameDisplay提供低分辨率亮度/色彩软化并复用缓存，开关存于display.txt。界面状态不含逐帧调试计数。

headless.cpp对Mapper 3的$8000–$FFFF和$6802均按原盘无总线冲突的方式换页。G122以程序字符串及$FFE0注入等待签名识别，仅在该等待范围内近似原盘的中段显示同步信号，不修改ROM；详情见REVERSE-ENGINEERING.md。

## 【V1.0 beta效果实现】

GameDisplay.MenuMode控制菜单的50%混合；游戏渲染路径未改动。OSD独立低分辨率透明层柔化。Player.LowQuality与界面开关同步，VcdAudioFilter保存跨缓冲状态，关闭时直接使用原始音频。默认仿真开启，但保留已有display.txt选择。

## 【V1.0 beta效果实现】

GameDisplay.MenuMode控制菜单的50%混合；游戏渲染路径未改动。OSD独立低分辨率透明层柔化。Player.LowQuality与界面开关同步，VcdAudioFilter保存跨缓冲状态，关闭时直接使用原始音频。默认仿真开启，但保留已有display.txt选择。
