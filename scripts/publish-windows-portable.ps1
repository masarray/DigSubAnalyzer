param(
    [string]$Version = "1.2.0-public-beta",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppName = "ProcessBusInsight",
    [string]$ProjectPath = "src/ProcessBus.App.Wpf/ProcessBus.App.Wpf.csproj",
    [string]$OutputRoot = "artifacts",
    [switch]$FrameworkDependent,
    [switch]$MultiFile
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version cannot be empty."
}

$portableName = "$AppName-v$Version-$Runtime-portable"
$publishDir = Join-Path $repoRoot "$OutputRoot/publish/$portableName/app"
$stageDir = Join-Path $repoRoot "$OutputRoot/package/$portableName"
$releaseDir = Join-Path $repoRoot "$OutputRoot/release"
$zipPath = Join-Path $releaseDir "$portableName.zip"
$shaPath = Join-Path $releaseDir "SHA256SUMS.txt"
$selfContained = -not $FrameworkDependent.IsPresent
$singleFile = -not $MultiFile.IsPresent

Write-Host "Publishing $AppName $Version for $Runtime"
Write-Host "Self-contained: $selfContained"
Write-Host "Single-file EXE: $singleFile"

Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir, $stageDir, $releaseDir | Out-Null

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path

# Runtime-specific restore is done explicitly, then publish runs with --no-restore.
# Do not pass /p:AssemblyName from the command line. MSBuild global properties flow into
# project references too, which can make every project in the graph look like the same
# project name to NuGet restore and trigger: Ambiguous project name 'ProcessBusInsight'.
$restoreArgs = @(
    "restore", $resolvedProjectPath,
    "-r", $Runtime,
    "/p:ContinuousIntegrationBuild=true"
)

dotnet @restoreArgs

$publishArgs = @(
    "publish", $resolvedProjectPath,
    "--no-restore",
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $publishDir,
    "/p:PublishSingleFile=$($singleFile.ToString().ToLowerInvariant())",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:PublishTrimmed=false",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:ErrorOnDuplicatePublishOutputFiles=true"
)

if ($selfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
}
else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

dotnet @publishArgs

$appStageDir = Join-Path $stageDir "app"
New-Item -ItemType Directory -Path $appStageDir | Out-Null

$publishedExe = Join-Path $publishDir "$AppName.exe"
if (-not (Test-Path $publishedExe)) {
    $fallbackExe = Get-ChildItem -Path $publishDir -Filter "*.exe" -File | Select-Object -First 1
    if ($fallbackExe) {
        $publishedExe = $fallbackExe.FullName
    }
}

if ($singleFile) {
    if (-not (Test-Path $publishedExe)) {
        throw "Single-file publish did not produce an executable in: $publishDir"
    }
    Copy-Item -Path $publishedExe -Destination (Join-Path $appStageDir "$AppName.exe") -Force
}
else {
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $appStageDir -Recurse -Force
    $exeInApp = Join-Path $appStageDir "$AppName.exe"
    if (-not (Test-Path $exeInApp) -and (Test-Path $publishedExe)) {
        Copy-Item $publishedExe $exeInApp -Force
    }
}

$quickStart = @"
Process Bus Insight / DigSubAnalyzer
Windows portable package
Version: $Version

1. Install Npcap on the Windows machine that will capture IEC 61850 Process Bus traffic.
2. Extract this ZIP to a local folder, for example C:\Tools\ProcessBusInsight.
3. Run app\$AppName.exe or Start-ProcessBusInsight.bat. The app folder is intentionally published as a single self-contained EXE by default.
4. Select a real physical Ethernet adapter connected to a TAP, mirror port, or isolated test network.
5. Start capture and review SV, GOOSE, PTP, diagnostics, and SCL binding views.

Timing note:
Normal Windows/Npcap timestamps are software based. Use timing findings as screening evidence unless the capture path is validated with hardware timestamping, TAP, or trusted timing equipment.

Documentation:
https://github.com/masarray/DigSubAnalyzer#readme
"@

Set-Content -Path (Join-Path $stageDir "README_QUICK_START.txt") -Value $quickStart -Encoding UTF8

$launcher = @"
@echo off
setlocal
cd /d "%~dp0"
if exist "app\$AppName.exe" (
  start "Process Bus Insight" "app\$AppName.exe"
) else (
  echo Process Bus Insight executable was not found in the app folder.
  pause
)
"@
Set-Content -Path (Join-Path $stageDir "Start-ProcessBusInsight.bat") -Value $launcher -Encoding ASCII

$copyMap = @(
    @{ Source = "LICENSE"; Required = $true },
    @{ Source = "NOTICE"; Required = $false },
    @{ Source = "THIRD_PARTY_NOTICES.md"; Required = $false },
    @{ Source = "docs/QUICK_START.md"; Required = $false },
    @{ Source = "docs/TROUBLESHOOTING.md"; Required = $false }
)

foreach ($item in $copyMap) {
    $source = Join-Path $repoRoot $item.Source
    if (Test-Path $source) {
        Copy-Item $source -Destination $stageDir -Force
    }
    elseif ($item.Required) {
        throw "Required package file missing: $($item.Source)"
    }
}

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$shaLine = "$($hash.Hash.ToLowerInvariant())  $(Split-Path $zipPath -Leaf)"
Set-Content -Path $shaPath -Value $shaLine -Encoding ASCII

Write-Host "Created: $zipPath"
Write-Host "Created: $shaPath"
