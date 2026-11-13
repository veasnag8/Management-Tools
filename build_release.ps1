$src = "D:\Project\Release_v1.2.0\SNA-Dubber"
$outputDir = "D:\Project\Release_v1.2.0\Output"
$patchZip = Join-Path $outputDir "ToolDubber-Patch-v2.1.1.zip"
$stage = Join-Path $env:TEMP "patch_stage_v211"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $stage "Engine") -Force | Out-Null
Copy-Item (Join-Path $src "SnaDubber.UIHook.dll") $stage -Force
Get-ChildItem (Join-Path $src "Engine") -File | Where-Object { $_.Extension -in ".py",".txt" } | Copy-Item -Destination (Join-Path $stage "Engine") -Force

if (Test-Path $patchZip) { Remove-Item $patchZip -Force }
Compress-Archive -Path "$stage\*" -DestinationPath $patchZip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$ph = (Get-FileHash $patchZip -Algorithm SHA256).Hash.ToLower()
$psz = (Get-Item $patchZip).Length
Write-Host "PATCH_SHA256=$ph"
Write-Host "PATCH_SIZE=$psz"

$iscc = "C:\Users\song.veasna.MAM\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
$iss = "D:\Project\Release_v1.2.0\Management-Tools\installer\setup.iss"

& $iscc $iss
if ($LASTEXITCODE -eq 0) {
    $out = Join-Path $outputDir "ToolDubber-Setup-v2.1.1.exe"
    $h = (Get-FileHash $out -Algorithm SHA256).Hash.ToLower()
    $sz = (Get-Item $out).Length
    Write-Host "EXE_SHA256=$h"
    Write-Host "EXE_SIZE=$sz"
} else {
    Write-Error "ISCC compilation failed with code $LASTEXITCODE"
}
