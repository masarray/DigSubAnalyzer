[CmdletBinding()]
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
Set-StrictMode -Version Latest
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

function Get-GitText {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = & git -C $repoRoot @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed.`n$($output -join [Environment]::NewLine)"
    }
    ($output -join '').Trim()
}

function Invoke-OptionalAuthenticodeSigning {
    param([Parameter(Mandatory)][string]$FilePath)

    if ([string]::IsNullOrWhiteSpace($env:WINDOWS_SIGNING_CERTIFICATE_BASE64)) {
        Write-Warning "Authenticode signing certificate is not configured. The package will be unsigned."
        return 'unsigned'
    }
    if ([string]::IsNullOrWhiteSpace($env:WINDOWS_SIGNING_CERTIFICATE_PASSWORD)) {
        throw "WINDOWS_SIGNING_CERTIFICATE_PASSWORD is required when a signing certificate is configured."
    }

    $certificatePath = Join-Path $env:RUNNER_TEMP "processbus-signing-$([Guid]::NewGuid().ToString('N')).pfx"
    try {
        [System.IO.File]::WriteAllBytes($certificatePath, [Convert]::FromBase64String($env:WINDOWS_SIGNING_CERTIFICATE_BASE64))
        $securePassword = ConvertTo-SecureString $env:WINDOWS_SIGNING_CERTIFICATE_PASSWORD -AsPlainText -Force
        $certificate = Get-PfxCertificate -FilePath $certificatePath -Password $securePassword
        if (-not $certificate.HasPrivateKey) {
            throw "The configured signing certificate does not contain a private key."
        }

        $signature = Set-AuthenticodeSignature -FilePath $FilePath -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "Authenticode signing failed: $($signature.Status) - $($signature.StatusMessage)"
        }
        Write-Host "Authenticode signature applied: $($certificate.Subject)"
        return 'valid'
    }
    finally {
        Remove-Item -LiteralPath $certificatePath -Force -ErrorAction SilentlyContinue
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) { throw "Version cannot be empty." }

$requiredReleaseDocs = @(
    "docs/QUICK_START.pdf", "docs/USER_MANUAL.pdf", "LICENSE", "NOTICE",
    "COMMERCIAL-LICENSE.md", "COPYRIGHT.md", "TRADEMARK.md",
    "THIRD_PARTY_NOTICES.md", "docs/LICENSING.md", "docs/ASSET_PROVENANCE.md"
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
$sbomReleasePath = Join-Path $releaseDir "ProcessBusInsight-SBOM.cdx.json"
$sourceReleasePath = Join-Path $releaseDir "SOURCE.md"
$shaPath = Join-Path $releaseDir "SHA256SUMS.txt"
$selfContained = -not $FrameworkDependent.IsPresent
$singleFile = -not $MultiFile.IsPresent

Write-Host "Publishing $AppName $Version for $Runtime"
Write-Host "Community license: GPL-3.0-or-later"
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDir, $stageDir, $releaseDir -Force | Out-Null

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
dotnet restore $resolvedProjectPath -r $Runtime /p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw "Runtime-specific restore failed." }

$builtCommit = Get-GitText @('rev-parse','HEAD')
$shortCommit = $builtCommit.Substring(0, [Math]::Min(12, $builtCommit.Length))
$versionParts = $Version.Split('-', 2)
$versionPrefix = $versionParts[0]
$versionSuffix = if ($versionParts.Length -gt 1) { $versionParts[1] } else { '' }
$publishArgs = @(
    "publish", $resolvedProjectPath, "--no-restore", "-c", $Configuration,
    "-r", $Runtime, "-o", $publishDir,
    "/p:Version=$Version", "/p:VersionPrefix=$versionPrefix", "/p:VersionSuffix=$versionSuffix",
    "/p:PackageVersion=$Version", "/p:AssemblyVersion=$versionPrefix.0",
    "/p:FileVersion=$versionPrefix.0", "/p:InformationalVersion=$Version+$shortCommit",
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

$stagedExe = Join-Path $stageDir "$AppName.exe"
$signatureStatus = if (Test-Path $stagedExe) { Invoke-OptionalAuthenticodeSigning $stagedExe } else { 'multi-file' }

$sourceHeadCommit = if ([string]::IsNullOrWhiteSpace($env:SOURCE_HEAD_COMMIT)) { $builtCommit } else { $env:SOURCE_HEAD_COMMIT }
$testedMergeCommit = if ([string]::IsNullOrWhiteSpace($env:TESTED_MERGE_COMMIT)) { $builtCommit } else { $env:TESTED_MERGE_COMMIT }
$sourceRef = if ([string]::IsNullOrWhiteSpace($env:SOURCE_REF)) { Get-GitText @('rev-parse','--abbrev-ref','HEAD') } else { $env:SOURCE_REF }
$workflowUrl = if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_RUN_ID) -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) {
    "https://github.com/$($env:GITHUB_REPOSITORY)/actions/runs/$($env:GITHUB_RUN_ID)"
} else {
    'Local build - no GitHub Actions run URL'
}

$sourceOffer = @"
# Corresponding Source and Build Identity

Product: Process Bus Insight / DigSubAnalyzer
Version: $Version
Community license: GPL-3.0-or-later
Built commit: $builtCommit
Source-head commit: $sourceHeadCommit
Tested merge commit: $testedMergeCommit
Source ref: $sourceRef
Authenticode status: $signatureStatus

Exact source tree:
https://github.com/masarray/DigSubAnalyzer/tree/$builtCommit

Immutable source archive for the built commit:
https://github.com/masarray/DigSubAnalyzer/archive/$builtCommit.zip

Repository:
https://github.com/masarray/DigSubAnalyzer

Build workflow:
$workflowUrl

This file identifies the source corresponding to this binary package. Retain it with the package. Historical Apache-2.0 grants remain attached only to the revisions to which they originally applied; this post-transition package is GPL-3.0-or-later.
"@
[System.IO.File]::WriteAllText((Join-Path $stageDir 'SOURCE.md'), $sourceOffer, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($sourceReleasePath, $sourceOffer, [System.Text.UTF8Encoding]::new($false))

& (Join-Path $PSScriptRoot 'generate-sbom.ps1') -Version $Version -OutputPath $sbomReleasePath
Copy-Item $sbomReleasePath (Join-Path $stageDir 'sbom.cdx.json') -Force

$packageReadme = @"
Process Bus Insight / DigSubAnalyzer
Windows portable package
Version: $Version
Community license: GPL-3.0-or-later

1. Install Npcap separately when live raw Ethernet capture is required.
2. Extract this ZIP to a local folder.
3. Run $AppName.exe.
4. Confirm the version, GPL license, and build commit in About.
5. Use an authorized TAP, mirror port, or isolated engineering test network.

Included integrity and legal documents:
LICENSE.txt, NOTICE.txt, COMMERCIAL-LICENSE.md, COPYRIGHT.md, TRADEMARK.md,
THIRD_PARTY_NOTICES.md, Licensing.md, Asset Provenance.md, SOURCE.md, and sbom.cdx.json.

Proprietary, OEM, white-label, closed-source redistribution, private-branch, or contractual service rights require a separate negotiated agreement. The commercial notice grants no additional rights by itself.

Windows/Npcap timestamps are screening evidence unless independently validated. The software does not prove external IED acceptance or process action.

https://github.com/masarray/DigSubAnalyzer
"@
[System.IO.File]::WriteAllText((Join-Path $stageDir "README.txt"), $packageReadme, [System.Text.UTF8Encoding]::new($false))

$copyMap = @(
    @{ Source = "LICENSE"; Destination = "LICENSE.txt" },
    @{ Source = "NOTICE"; Destination = "NOTICE.txt" },
    @{ Source = "COMMERCIAL-LICENSE.md"; Destination = "COMMERCIAL-LICENSE.md" },
    @{ Source = "COPYRIGHT.md"; Destination = "COPYRIGHT.md" },
    @{ Source = "TRADEMARK.md"; Destination = "TRADEMARK.md" },
    @{ Source = "THIRD_PARTY_NOTICES.md"; Destination = "THIRD_PARTY_NOTICES.md" },
    @{ Source = "docs/LICENSING.md"; Destination = "Licensing.md" },
    @{ Source = "docs/ASSET_PROVENANCE.md"; Destination = "Asset Provenance.md" },
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

$checksumFiles = @($zipPath, $sbomReleasePath, $sourceReleasePath)
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $(Split-Path $file -Leaf)"
}
[System.IO.File]::WriteAllLines($shaPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "Created: $zipPath"
Write-Host "Created: $sbomReleasePath"
Write-Host "Created: $sourceReleasePath"
Write-Host "Created: $shaPath"
