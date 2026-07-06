param(
    [string]$Version = "1.2.7",
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


$requiredReleaseDocs = @(
    "docs/QUICK_START.pdf",
    "docs/USER_MANUAL.pdf"
)

foreach ($doc in $requiredReleaseDocs) {
    $docPath = Join-Path $repoRoot $doc
    if (-not (Test-Path $docPath)) {
        throw "Release documentation is missing: $doc. These PDF files must be committed to the repository before running the release workflow. If you just copied the repo upgrade files locally, run: git add -f docs/QUICK_START.pdf docs/USER_MANUAL.pdf"
    }
}

$portableName = "$AppName-v$Version-$Runtime-portable"
$publishDir = Join-Path $repoRoot "$OutputRoot/publish/$portableName/publish"
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
# Do not pass /p:AssemblyName from the command line. MSBuild global properties flow
# into project references too and can trigger NuGet ambiguous project-name errors.
$restoreArgs = @(
    "restore", $resolvedProjectPath,
    "-r", $Runtime,
    "/p:ContinuousIntegrationBuild=true"
)

dotnet @restoreArgs

# Split "1.2.7" / "1.2.7-public-beta" into a numeric prefix (for AssemblyVersion,
# which must be four numeric parts) and an optional suffix.
$versionParts = $Version.Split('-', 2)
$versionPrefix = $versionParts[0]
$versionSuffix = if ($versionParts.Length -gt 1) { $versionParts[1] } else { '' }

$publishArgs = @(
    "publish", $resolvedProjectPath,
    "--no-restore",
    "-c", $Configuration,
    "-r", $Runtime,
    "-o", $publishDir,
    "/p:VersionPrefix=$versionPrefix",
    "/p:VersionSuffix=$versionSuffix",
    "/p:PackageVersion=$Version",
    "/p:AssemblyVersion=$versionPrefix.0",
    "/p:FileVersion=$versionPrefix.0",
    "/p:InformationalVersion=$Version",
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
    Copy-Item -Path $publishedExe -Destination (Join-Path $stageDir "$AppName.exe") -Force
}
else {
    $appStageDir = Join-Path $stageDir "app"
    New-Item -ItemType Directory -Path $appStageDir | Out-Null
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $appStageDir -Recurse -Force
}

$packageReadme = @"
Process Bus Insight / DigSubAnalyzer
Windows portable package
Version: $Version

How to run:
1. Install Npcap on the Windows machine that will capture IEC 61850 Process Bus traffic.
2. Extract this ZIP to a local folder, for example C:\Tools\ProcessBusInsight.
3. Run $AppName.exe.
4. Select a real physical Ethernet adapter connected to a TAP, mirror port, or isolated test network.
5. Start capture and review SV, GOOSE, PTP, diagnostics, and SCL binding views.

Included documents:
- Quick Start.pdf
- User Manual.pdf

Timing note:
Normal Windows/Npcap timestamps are software based. Use timing findings as screening evidence unless the capture path is validated with hardware timestamping, TAP, or trusted timing equipment.

Project page:
https://github.com/masarray/DigSubAnalyzer
"@
Set-Content -Path (Join-Path $stageDir "README.txt") -Value $packageReadme -Encoding UTF8

$copyMap = @(
    @{ Source = "LICENSE"; Destination = "LICENSE.txt"; Required = $true },
    @{ Source = "NOTICE"; Destination = "NOTICE.txt"; Required = $false },
    @{ Source = "THIRD_PARTY_NOTICES.md"; Destination = "THIRD_PARTY_NOTICES.md"; Required = $false },
    @{ Source = "docs/QUICK_START.pdf"; Destination = "Quick Start.pdf"; Required = $true },
    @{ Source = "docs/USER_MANUAL.pdf"; Destination = "User Manual.pdf"; Required = $true }
)

foreach ($item in $copyMap) {
    $source = Join-Path $repoRoot $item.Source
    if (Test-Path $source) {
        Copy-Item $source -Destination (Join-Path $stageDir $item.Destination) -Force
    }
    elseif ($item.Required) {
        throw "Required package file missing: $($item.Source). For release documentation PDFs, make sure they are committed with: git add -f docs/QUICK_START.pdf docs/USER_MANUAL.pdf"
    }
}

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$shaLine = "$($hash.Hash.ToLowerInvariant())  $(Split-Path $zipPath -Leaf)"
Set-Content -Path $shaPath -Value $shaLine -Encoding ASCII

Write-Host "Created: $zipPath"
Write-Host "Created: $shaPath"
