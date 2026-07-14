param(
    [string]$Version = "1.4.0-beta.2",
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
if ([string]::IsNullOrWhiteSpace($Version)) { throw "Version cannot be empty." }

$requiredReleaseDocs = @(
    "docs/QUICK_START.pdf", "docs/USER_MANUAL.pdf", "LICENSE", "NOTICE",
    "COMMERCIAL-LICENSE.md", "COPYRIGHT.md", "TRADEMARK.md",
    "THIRD_PARTY_NOTICES.md", "docs/LICENSING.md"
)
foreach ($doc in $requiredReleaseDocs) {
    if (-not (Test-Path (Join-Path $repoRoot $doc))) { throw "Required release document is missing: $doc" }
}

$licenseText = Get-Content (Join-Path $repoRoot "LICENSE") -Raw
if ($licenseText -notmatch 'GNU GENERAL PUBLIC LICENSE' -or $licenseText -notmatch 'Version 3, 29 June 2007') {
    throw "Current root LICENSE is not GNU GPL version 3."
}
if (Test-Path (Join-Path $repoRoot "LICENSE-APACHE-2.0")) {
    throw "Historical Apache license must not be staged as a current release license."
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
Write-Host "Community license: GPL-3.0-or-later"
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir, $stageDir, $releaseDir | Out-Null

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
dotnet restore $resolvedProjectPath -r $Runtime /p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw "Runtime-specific restore failed." }

$versionParts = $Version.Split('-', 2)
$versionPrefix = $versionParts[0]
$versionSuffix = if ($versionParts.Length -gt 1) { $versionParts[1] } else { '' }
$publishArgs = @(
    "publish", $resolvedProjectPath, "--no-restore", "-c", $Configuration,
    "-r", $Runtime, "-o", $publishDir,
    "/p:Version=$Version", "/p:VersionPrefix=$versionPrefix", "/p:VersionSuffix=$versionSuffix",
    "/p:PackageVersion=$Version", "/p:AssemblyVersion=$versionPrefix.0",
    "/p:FileVersion=$versionPrefix.0", "/p:InformationalVersion=$Version",
    "/p:PublishSingleFile=$($singleFile.ToString().ToLowerInvariant())",
    "/p:IncludeNativeLibrariesForSelfExtract=true", "/p:EnableCompressionInSingleFile=true",
    "/p:PublishTrimmed=false", "/p:DebugType=None", "/p:DebugSymbols=false",
    "/p:ErrorOnDuplicatePublishOutputFiles=true", "--self-contained", $selfContained.ToString().ToLowerInvariant()
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$publishedExe = Join-Path $publishDir "$AppName.exe"
if (-not (Test-Path $publishedExe)) {
    $fallbackExe = Get-ChildItem -Path $publishDir -Filter "*.exe" -File | Select-Object -First 1
    if ($fallbackExe) { $publishedExe = $fallbackExe.FullName }
}
if ($singleFile) {
    if (-not (Test-Path $publishedExe)) { throw "Single-file publish did not produce an executable in: $publishDir" }
    Copy-Item $publishedExe (Join-Path $stageDir "$AppName.exe") -Force
} else {
    $appStageDir = Join-Path $stageDir "app"
    New-Item -ItemType Directory -Path $appStageDir | Out-Null
    Copy-Item (Join-Path $publishDir "*") $appStageDir -Recurse -Force
}

$packageReadme = @"
Process Bus Insight / DigSubAnalyzer
Windows portable package
Version: $Version
Community license: GPL-3.0-or-later

1. Install Npcap separately when live raw Ethernet capture is required.
2. Extract this ZIP to a local folder.
3. Run $AppName.exe.
4. Use an authorized TAP, mirror port, or isolated engineering test network.

Included legal documents:
LICENSE.txt, NOTICE.txt, COMMERCIAL-LICENSE.md, COPYRIGHT.md, TRADEMARK.md,
THIRD_PARTY_NOTICES.md, and Licensing.md.

Proprietary, OEM, white-label, closed-source redistribution, private-branch, or contractual service rights require a separate negotiated agreement. The commercial notice grants no additional rights by itself.

Windows/Npcap timestamps are screening evidence unless independently validated. The software does not prove external IED acceptance or process action.

https://github.com/masarray/DigSubAnalyzer
"@
Set-Content (Join-Path $stageDir "README.txt") $packageReadme -Encoding UTF8

$copyMap = @(
    @{ Source = "LICENSE"; Destination = "LICENSE.txt" },
    @{ Source = "NOTICE"; Destination = "NOTICE.txt" },
    @{ Source = "COMMERCIAL-LICENSE.md"; Destination = "COMMERCIAL-LICENSE.md" },
    @{ Source = "COPYRIGHT.md"; Destination = "COPYRIGHT.md" },
    @{ Source = "TRADEMARK.md"; Destination = "TRADEMARK.md" },
    @{ Source = "THIRD_PARTY_NOTICES.md"; Destination = "THIRD_PARTY_NOTICES.md" },
    @{ Source = "docs/LICENSING.md"; Destination = "Licensing.md" },
    @{ Source = "docs/QUICK_START.pdf"; Destination = "Quick Start.pdf" },
    @{ Source = "docs/USER_MANUAL.pdf"; Destination = "User Manual.pdf" }
)
foreach ($item in $copyMap) {
    $source = Join-Path $repoRoot $item.Source
    if (-not (Test-Path $source)) { throw "Required package file missing: $($item.Source)" }
    Copy-Item $source (Join-Path $stageDir $item.Destination) -Force
}

if (Test-Path (Join-Path $stageDir "LICENSE-APACHE-2.0")) {
    throw "Historical Apache license must not appear in a current GPL package."
}

Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force
$hash = Get-FileHash $zipPath -Algorithm SHA256
Set-Content $shaPath "$($hash.Hash.ToLowerInvariant())  $(Split-Path $zipPath -Leaf)" -Encoding ASCII
Write-Host "Created: $zipPath"
Write-Host "Created: $shaPath"
