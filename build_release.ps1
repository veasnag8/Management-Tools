param(
    [string]$AppSourceDir = "C:\VEASNA\Tool Dubber",
    [string]$OutputDir = "C:\VEASNA\Release",
    [string]$InnoSetupCompiler = ""
)

$ErrorActionPreference = "Stop"
$projectFile = Join-Path $PSScriptRoot "SnaDubber.UIHook\SnaDubber.UIHook.csproj"
$engineSource = Join-Path $PSScriptRoot "Engine"
$manifestPath = Join-Path $PSScriptRoot "version.json"

[xml]$project = Get-Content $projectFile
$appVersion = $project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "Unable to read the application version from $projectFile"
}

$requiredFiles = @(
    "SnaDubber.exe",
    "SnaDubber.dll",
    "SnaDubber.UIHook.dll",
    "CommunityToolkit.Mvvm.dll",
    "Engine\engine_main.py",
    "Engine\dubbing_tts.py"
)
foreach ($file in $requiredFiles) {
    $path = Join-Path $AppSourceDir $file
    if (-not (Test-Path $path -PathType Leaf)) {
        throw "Required application release file was not found: $path"
    }
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($isccCommand) {
        $InnoSetupCompiler = $isccCommand.Source
    } else {
        $InnoSetupCompiler = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
    }
}
if (-not (Test-Path $InnoSetupCompiler -PathType Leaf)) {
    throw "Inno Setup compiler was not found: $InnoSetupCompiler"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$stageRoot = Join-Path $env:TEMP "tooldubber-release-v$appVersion-$([guid]::NewGuid().ToString('N'))"
$stageApp = Join-Path $stageRoot "app"
New-Item -ItemType Directory -Path $stageApp -Force | Out-Null

try {
    Get-ChildItem $AppSourceDir -Force |
        Where-Object {
            $_.Name -notin @(
                "Engine",
                "hook.log",
                "unins000.exe",
                "unins000.dat",
                "SnaDubber.dll.orig",
                "test_tts.json",
                "test_tts.mp3",
                "test.mp3"
            )
        } |
        Copy-Item -Destination $stageApp -Recurse -Force

    $stageEngine = Join-Path $stageApp "Engine"
    New-Item -ItemType Directory -Path $stageEngine -Force | Out-Null
    Get-ChildItem $engineSource -File |
        Where-Object {
            $_.Extension -in @(".py", ".txt") -and
            $_.Name -notin @("tts_test.py", "test_dubbing_tts.py")
        } |
        Copy-Item -Destination $stageEngine -Force

    & dotnet build $projectFile --configuration Release `
        "-p:SnaDubberAssemblyPath=$(Join-Path $AppSourceDir 'SnaDubber.dll')" `
        "-p:CommunityToolkitAssemblyPath=$(Join-Path $AppSourceDir 'CommunityToolkit.Mvvm.dll')"
    if ($LASTEXITCODE -ne 0) {
        throw "UIHook build failed with code $LASTEXITCODE"
    }

    $hookDll = Join-Path (Split-Path $projectFile -Parent) "bin\Release\net8.0-windows\SnaDubber.UIHook.dll"
    if (-not (Test-Path $hookDll -PathType Leaf)) {
        throw "Built UIHook assembly was not found: $hookDll"
    }
    Copy-Item $hookDll (Join-Path $stageApp "SnaDubber.UIHook.dll") -Force

    $patchZip = Join-Path $OutputDir "ToolDubber-Patch-v$appVersion.zip"
    $patchStage = Join-Path $stageRoot "patch"
    New-Item -ItemType Directory -Path (Join-Path $patchStage "Engine") -Force | Out-Null
    Copy-Item (Join-Path $stageApp "SnaDubber.UIHook.dll") $patchStage -Force
    Copy-Item (Join-Path $stageApp "SnaDubber.dll") $patchStage -Force
    Copy-Item (Join-Path $stageEngine "*") (Join-Path $patchStage "Engine") -Recurse -Force
    if (Test-Path $patchZip) {
        Remove-Item $patchZip -Force
    }
    Compress-Archive -Path (Join-Path $patchStage "*") -DestinationPath $patchZip -CompressionLevel Optimal
    $patchHash = (Get-FileHash $patchZip -Algorithm SHA256).Hash.ToLowerInvariant()
    $patchSize = (Get-Item $patchZip).Length

    $iss = Join-Path $PSScriptRoot "installer\setup.iss"
    $installerPath = Join-Path $OutputDir "ToolDubber-Setup-v$appVersion.exe"
    & $InnoSetupCompiler `
        "/DMyAppVersion=$appVersion" `
        "/DMyAppSourceDir=$stageApp" `
        "/DMyOutputDir=$OutputDir" `
        $iss
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with code $LASTEXITCODE"
    }
    if (-not (Test-Path $installerPath -PathType Leaf)) {
        throw "Installer was not created: $installerPath"
    }
    $installerHash = (Get-FileHash $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.latest_version = $appVersion
    $manifest.download_url = "https://github.com/veasnag8/Management-Tools/releases/download/v$appVersion/ToolDubber-Setup-v$appVersion.exe"
    $manifest.patch_url = "https://github.com/veasnag8/Management-Tools/releases/download/v$appVersion/ToolDubber-Patch-v$appVersion.zip"
    $manifest.patch_sha256 = $patchHash
    $manifest.patch_size_bytes = $patchSize
    $manifest.sha256 = $installerHash
    $manifest.release_notes = "Version ${appVersion}: Fixes an unexpected app close during translation caused by text-layer UI elements being repeatedly attached until a layout stack overflow. Also fixes the no-overlap status button styling, Srey Mom voice changes, overlap highlighting and navigation, start/end time editing, and removes Gemini Keys buttons."
    $manifestJson = $manifest | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($manifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))

    Write-Host "Version: $appVersion"
    Write-Host "Installer: $installerPath"
    Write-Host "Installer SHA256: $installerHash"
    Write-Host "Patch: $patchZip"
    Write-Host "Patch SHA256: $patchHash"
    Write-Host "Patch size: $patchSize"
    Write-Host "Updated version.json. Upload both release assets before pushing the manifest."
}
finally {
    if (Test-Path $stageRoot) {
        Remove-Item $stageRoot -Recurse -Force
    }
}
