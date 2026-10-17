$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $PSScriptRoot '..\VCD300.exe'
& $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ "/win32icon:$(Join-Path $PSScriptRoot 'AppIcon.ico')" "/resource:$(Join-Path $PSScriptRoot 'AppIcon.ico'),VCD300.AppIcon.ico" "/out:$output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll (Join-Path $PSScriptRoot 'VCD300.cs') (Join-Path $PSScriptRoot 'NativePlayer.cs') (Join-Path $PSScriptRoot 'InputDevices.cs') (Join-Path $PSScriptRoot 'WindowGeometry.cs')
if($LASTEXITCODE -ne 0){throw 'Build failed'}
