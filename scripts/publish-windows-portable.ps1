param(
    [string]$Version = "1.4.1",
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

# Source identity must be unique even when successive test installers use 1.4.1.
$sourceCommit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -cnotmatch '^[a-f0-9]{40}

$requiredReleaseDocs = @(
    "docs/QUICK_START.md", "docs/USER_MANUAL.md", "docs/INSTALLATION_AND_UPDATE.md", "LICENSE", "NOTICE",
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

# Release PDFs are rendered fresh from the canonical Markdown source and
# cryptographically tied to the two Markdown files in the same installer.
# Old tracked PDFs must never silently enter a new public package.
$manualDir = Join-Path $repoRoot "$OutputRoot/generated-docs"
$manualManifestPath = Join-Path $manualDir "manual-manifest.json"
if (-not (Test-Path -LiteralPath $manualManifestPath)) {
    throw "Current offline PDF manual manifest missing. Run python scripts/render-offline-manuals.py first."
}
$manualManifest = Get-Content -LiteralPath $manualManifestPath -Raw | ConvertFrom-Json
if ($manualManifest.format -ne "ProcessBusInsight-offline-manuals-v1") { throw 'Unsupported manual manifest format.' }
foreach ($entry in $manualManifest.files) {
    if ($entry.source -notin @("QUICK_START.md", "USER_MANUAL.md")) {
        throw "Unexpected manual source: $($entry.source)"
    }
    $src = Join-Path $repoRoot ("docs/" + $entry.source)
    $pdf = Join-Path $manualDir $entry.pdf
    if (-not (Test-Path -LiteralPath $pdf)) { throw "Missing generated PDF $pdf" }
    $sourceHash = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash
    $pdfHash = (Get-FileHash -LiteralPath $pdf -Algorithm SHA256).Hash
    if ($sourceHash -ne $entry.sourceSha256 -or $pdfHash -ne $entry.pdfSha256) {
        throw "Offline PDF not synchronized with $($entry.source); regenerate manuals."
    }
}
if (@($manualManifest.files).Count -ne 2) { throw "Exactly two offline PDF manuals are required." }

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

# Physical acceptance needs a reproducible capture-vs-SCL inspector, not only
# a Windows UI EXE. Publish the existing read-only CLI self-contained into a
# separate tools folder; never stage raw PCAP/SCL or customer identities.
$interopProject = Join-Path $repoRoot "tools/ProcessBus.InteropCli/ProcessBus.InteropCli.csproj"
$interopPublishDir = Join-Path $repoRoot "$OutputRoot/publish/$portableName/interop-cli"
$interopStageDir = Join-Path $stageDir "Bench"
New-Item -ItemType Directory -Path $interopStageDir -Force | Out-Null
dotnet restore $interopProject -r $Runtime /p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw "Interop CLI runtime-specific restore failed." }
$interopPublishArgs = @(
    "publish", $interopProject, "--no-restore", "-c", $Configuration,
    "-r", $Runtime, "-o", $interopPublishDir,
    "/p:Version=$Version", "/p:VersionPrefix=$versionPrefix", "/p:VersionSuffix=$versionSuffix",
    "/p:PublishSingleFile=true", "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true", "/p:PublishTrimmed=false",
    "/p:DebugType=None", "/p:DebugSymbols=false",
    "/p:ErrorOnDuplicatePublishOutputFiles=true", "--self-contained", "true"
)
dotnet @interopPublishArgs
if ($LASTEXITCODE -ne 0) { throw "Interop CLI publish failed." }
$interopExe = Join-Path $interopPublishDir "ProcessBus.InteropCli.exe"
if (-not (Test-Path $interopExe)) { throw "Interop CLI publish produced no executable." }
Copy-Item $interopExe (Join-Path $interopStageDir "ProcessBus.InteropCli.exe") -Force
Copy-Item (Join-Path $repoRoot "docs/M4_ARSTACK_INTEROP_ACCEPTANCE.md") (Join-Path $interopStageDir "M4_ARSTACK_INTEROP_ACCEPTANCE.md") -Force
Copy-Item (Join-Path $repoRoot "scripts/measure-m4-bench.ps1") (Join-Path $interopStageDir "measure-m4-bench.ps1") -Force
Copy-Item (Join-Path $repoRoot "docs/PHYSICAL_BENCH_QUICK_START.md") (Join-Path $interopStageDir "START_HERE.md") -Force

$packageReadme = @"
Process Bus Insight / DigSubAnalyzer
Windows self-contained distribution payload
Version: $Version
Community license: GPL-3.0-or-later

This payload is packaged both by the Inno Setup installer (recommended)
and as a separate optional portable ZIP.

INSTALLER: Run the official win-x64-setup.exe. It installs this payload,
Start Menu shortcuts, offline manuals and uninstaller to Program Files.
UPDATE: Close the analyzer and run the newer installer (no uninstall needed).
PORTABLE: Extract the ZIP completely and run $AppName.exe.

1. Install Npcap separately when live raw Ethernet capture is required.
   Missing/incompatible native Npcap must not produce a fake usable adapter.
2. Use an authorized TAP, mirror port, or isolated engineering test network.
3. Read Quick Start.pdf, User Manual.pdf and Install and Update.md offline.
   Current Markdown editions are included and cryptographically bound to PDFs
   through ManualSources.json.
4. For physical SV/PTP evidence read Bench/START_HERE.md.
5. Bench/ProcessBus.InteropCli.exe works offline without the .NET SDK.
6. Bench/measure-m4-bench.ps1 records local process CPU/memory.
   A diagnostic broadcast mirror does NOT prove the canonical VLAN SV path.

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
    @{ Source = "$OutputRoot/generated-docs/Quick Start.pdf"; Destination = "Quick Start.pdf" },
    @{ Source = "$OutputRoot/generated-docs/User Manual.pdf"; Destination = "User Manual.pdf" },
    @{ Source = "$OutputRoot/generated-docs/manual-manifest.json"; Destination = "ManualSources.json" },
    @{ Source = "docs/QUICK_START.md"; Destination = "Quick Start.md" },
    @{ Source = "docs/USER_MANUAL.md"; Destination = "User Manual.md" },
    @{ Source = "docs/INSTALLATION_AND_UPDATE.md"; Destination = "Install and Update.md" }
)
foreach ($item in $copyMap) {
    $source = Join-Path $repoRoot $item.Source
    if (-not (Test-Path $source)) { throw "Required package file missing: $($item.Source)" }
    Copy-Item $source (Join-Path $stageDir $item.Destination) -Force
}

if (Test-Path (Join-Path $stageDir "LICENSE-APACHE-2.0")) {
    throw "Historical Apache license must not appear in a current GPL package."
}

# Include SHA-linked provenance beside the EXE, both in Inno and portable
# artifacts, so operators can identify a stale or alternate install path.
$payloadProvenance = [ordered]@{
    schema = "processbus-build-v1"
    version = $Version
    commit = $sourceCommit
    executableSha256 = (Get-FileHash -LiteralPath (Join-Path $stageDir "$AppName.exe") -Algorithm SHA256).Hash.ToLowerInvariant()
    githubRunId = if ($env:GITHUB_RUN_ID) { $env:GITHUB_RUN_ID } else { "" }
}
$payloadProvenance | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $stageDir "BuildInfo.json") -Encoding UTF8

Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force
$hash = Get-FileHash $zipPath -Algorithm SHA256
Set-Content $shaPath "$($hash.Hash.ToLowerInvariant())  $(Split-Path $zipPath -Leaf)" -Encoding ASCII
Write-Host "Created: $zipPath"
Write-Host "Created: $shaPath"
) {
    throw "Packaging requires a Git checkout and source commit SHA."
}

$requiredReleaseDocs = @(
    "docs/QUICK_START.md", "docs/USER_MANUAL.md", "docs/INSTALLATION_AND_UPDATE.md", "LICENSE", "NOTICE",
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

# Release PDFs are rendered fresh from the canonical Markdown source and
# cryptographically tied to the two Markdown files in the same installer.
# Old tracked PDFs must never silently enter a new public package.
$manualDir = Join-Path $repoRoot "$OutputRoot/generated-docs"
$manualManifestPath = Join-Path $manualDir "manual-manifest.json"
if (-not (Test-Path -LiteralPath $manualManifestPath)) {
    throw "Current offline PDF manual manifest missing. Run python scripts/render-offline-manuals.py first."
}
$manualManifest = Get-Content -LiteralPath $manualManifestPath -Raw | ConvertFrom-Json
if ($manualManifest.format -ne "ProcessBusInsight-offline-manuals-v1") { throw 'Unsupported manual manifest format.' }
foreach ($entry in $manualManifest.files) {
    if ($entry.source -notin @("QUICK_START.md", "USER_MANUAL.md")) {
        throw "Unexpected manual source: $($entry.source)"
    }
    $src = Join-Path $repoRoot ("docs/" + $entry.source)
    $pdf = Join-Path $manualDir $entry.pdf
    if (-not (Test-Path -LiteralPath $pdf)) { throw "Missing generated PDF $pdf" }
    $sourceHash = (Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash
    $pdfHash = (Get-FileHash -LiteralPath $pdf -Algorithm SHA256).Hash
    if ($sourceHash -ne $entry.sourceSha256 -or $pdfHash -ne $entry.pdfSha256) {
        throw "Offline PDF not synchronized with $($entry.source); regenerate manuals."
    }
}
if (@($manualManifest.files).Count -ne 2) { throw "Exactly two offline PDF manuals are required." }

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

# Physical acceptance needs a reproducible capture-vs-SCL inspector, not only
# a Windows UI EXE. Publish the existing read-only CLI self-contained into a
# separate tools folder; never stage raw PCAP/SCL or customer identities.
$interopProject = Join-Path $repoRoot "tools/ProcessBus.InteropCli/ProcessBus.InteropCli.csproj"
$interopPublishDir = Join-Path $repoRoot "$OutputRoot/publish/$portableName/interop-cli"
$interopStageDir = Join-Path $stageDir "Bench"
New-Item -ItemType Directory -Path $interopStageDir -Force | Out-Null
dotnet restore $interopProject -r $Runtime /p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw "Interop CLI runtime-specific restore failed." }
$interopPublishArgs = @(
    "publish", $interopProject, "--no-restore", "-c", $Configuration,
    "-r", $Runtime, "-o", $interopPublishDir,
    "/p:Version=$Version", "/p:VersionPrefix=$versionPrefix", "/p:VersionSuffix=$versionSuffix",
    "/p:PublishSingleFile=true", "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true", "/p:PublishTrimmed=false",
    "/p:DebugType=None", "/p:DebugSymbols=false",
    "/p:ErrorOnDuplicatePublishOutputFiles=true", "--self-contained", "true"
)
dotnet @interopPublishArgs
if ($LASTEXITCODE -ne 0) { throw "Interop CLI publish failed." }
$interopExe = Join-Path $interopPublishDir "ProcessBus.InteropCli.exe"
if (-not (Test-Path $interopExe)) { throw "Interop CLI publish produced no executable." }
Copy-Item $interopExe (Join-Path $interopStageDir "ProcessBus.InteropCli.exe") -Force
Copy-Item (Join-Path $repoRoot "docs/M4_ARSTACK_INTEROP_ACCEPTANCE.md") (Join-Path $interopStageDir "M4_ARSTACK_INTEROP_ACCEPTANCE.md") -Force
Copy-Item (Join-Path $repoRoot "scripts/measure-m4-bench.ps1") (Join-Path $interopStageDir "measure-m4-bench.ps1") -Force
Copy-Item (Join-Path $repoRoot "docs/PHYSICAL_BENCH_QUICK_START.md") (Join-Path $interopStageDir "START_HERE.md") -Force

$packageReadme = @"
Process Bus Insight / DigSubAnalyzer
Windows self-contained distribution payload
Version: $Version
Community license: GPL-3.0-or-later

This payload is packaged both by the Inno Setup installer (recommended)
and as a separate optional portable ZIP.

INSTALLER: Run the official win-x64-setup.exe. It installs this payload,
Start Menu shortcuts, offline manuals and uninstaller to Program Files.
UPDATE: Close the analyzer and run the newer installer (no uninstall needed).
PORTABLE: Extract the ZIP completely and run $AppName.exe.

1. Install Npcap separately when live raw Ethernet capture is required.
   Missing/incompatible native Npcap must not produce a fake usable adapter.
2. Use an authorized TAP, mirror port, or isolated engineering test network.
3. Read Quick Start.pdf, User Manual.pdf and Install and Update.md offline.
   Current Markdown editions are included and cryptographically bound to PDFs
   through ManualSources.json.
4. For physical SV/PTP evidence read Bench/START_HERE.md.
5. Bench/ProcessBus.InteropCli.exe works offline without the .NET SDK.
6. Bench/measure-m4-bench.ps1 records local process CPU/memory.
   A diagnostic broadcast mirror does NOT prove the canonical VLAN SV path.

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
    @{ Source = "$OutputRoot/generated-docs/Quick Start.pdf"; Destination = "Quick Start.pdf" },
    @{ Source = "$OutputRoot/generated-docs/User Manual.pdf"; Destination = "User Manual.pdf" },
    @{ Source = "$OutputRoot/generated-docs/manual-manifest.json"; Destination = "ManualSources.json" },
    @{ Source = "docs/QUICK_START.md"; Destination = "Quick Start.md" },
    @{ Source = "docs/USER_MANUAL.md"; Destination = "User Manual.md" },
    @{ Source = "docs/INSTALLATION_AND_UPDATE.md"; Destination = "Install and Update.md" }
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
