$ErrorActionPreference='Stop'
$manifest=Get-Content -Raw (Join-Path $PSScriptRoot 'core-overrides.json') | ConvertFrom-Json
$target=Join-Path $PSScriptRoot 'core-source\fceux-fceux-2.7.0'
if(Test-Path -LiteralPath $target){throw 'Target already exists. Keep it intact and choose a fresh source checkout before preparing again.'}
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
& git clone --no-checkout $manifest.upstream $target
if($LASTEXITCODE -ne 0){throw 'Upstream clone failed'}
& git -C $target checkout --detach $manifest.commit
if($LASTEXITCODE -ne 0){throw 'Pinned upstream checkout failed'}
foreach($entry in $manifest.sha256.PSObject.Properties){
 $source=Join-Path (Join-Path $PSScriptRoot 'core-overrides') $entry.Name
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw ('Override hash mismatch: '+$entry.Name)}
 Copy-Item -LiteralPath $source -Destination (Join-Path $target $entry.Name)
}
Write-Host 'FCEUX 2.7.0 sources prepared with the exact V1.0 beta overrides. Build with build-msvc.py from a VS x64 developer shell.'
