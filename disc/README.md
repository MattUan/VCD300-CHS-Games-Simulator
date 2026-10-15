# 【中文游戏300 · 原始光盘镜像】

[下载 ISO 压缩包](https://github.com/MattUan/VCD300-CHS-Games-Simulator/raw/refs/heads/main/disc/Chinese-Games-300.iso.zip)，完整解压后得到 `Chinese-Games-300.iso`。压缩不改变镜像内容。

## 【校验】

原始 ISO：57,158,304 字节，SHA-256：

```
1998cb8675e6ec10afbf052be9fc1329405a0dcb99708349c0be610c34e42d63
```

Windows PowerShell：

```powershell
Get-FileHash .\Chinese-Games-300.iso -Algorithm SHA256
```

## 【提取游戏数据】

安装 Python 3，在仓库根目录运行：

```powershell
python .\source\convert_disc.py .\disc\Chinese-Games-300.iso .
```

脚本要求原始2352字节扇区镜像，提取300个菜单项对应的游戏ROM至 `roms`，并生成 `tests/disc-layout.json` 和 `tests/disc-layout.csv`。这些自动生成的报告会覆盖同名报告，如需保留原报告，请将最后一个参数改为新的输出目录。

这一步仅处理ROM，不编译应用或内核。菜单可按下一节准备。原始镜像无需在日常运行时挂载。

## 【菜单图片】

[下载 menus.7z](https://github.com/MattUan/VCD300-CHS-Games-Simulator/raw/refs/heads/main/disc/menus.7z)，内含 `01.png` 至 `30.png`，不带外层目录。请将30张图片解压到程序根目录的 `menus` 文件夹，最终路径例如 `menus/01.png`。

已核验全部30张图片与 V1.0 beta 使用的菜单逐字节一致。菜单压缩包SHA-256：

```
435522a1e8b424aeb4a94f0cba9f935449d213955ad871aeb6bfdc7fd94ca8fd
```

Windows 自带 tar 支持本压缩包；在仓库根目录可执行：

```powershell
New-Item -ItemType Directory -Force .\menus | Out-Null
tar -xf .\disc\menus.7z -C .\menus
```

已有菜单目录时，请先确认是否需要保留原图片；解压会覆盖同名文件。

## 【素材声明】

镜像由项目开发者提供，保留原盘数据；软件GPL许可不适用于光盘中的游戏及菜单素材，其权利归各自权利人所有。
