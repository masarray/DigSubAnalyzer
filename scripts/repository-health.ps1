param(
    [string]$ExpectedVersion = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

function Assert-Path([string]$Path) {
    if (-not (Test-Path (Join-Path $repoRoot $Path))) {
        throw "Required repository file is missing: $Path"
    }
}

function Read-RepoText([string]$Path) {
    return Get-Content -LiteralPath (Join-Path $repoRoot $Path) -Raw
}

$required = @(
    "ProcessBusSuite.sln",
    "Directory.Build.props",
    "README.md",
    "LICENSE",
    "NOTICE",
    "COMMERCIAL-LICENSE.md",
    "COPYRIGHT.md",
    "TRADEMARK.md",
    "CONTRIBUTOR-LICENSE-AGREEMENT.md",
    "DCO.txt",
    "THIRD_PARTY_NOTICES.md",
    "SECURITY.md",
    "CONTRIBUTING.md",
    "CODE_OF_CONDUCT.md",
    "SUPPORT.md",
    "CHANGELOG.md",
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    ".github/CODEOWNERS",
    ".github/pull_request_template.md",
    ".github/workflows/ci.yml",
    ".github/workflows/runtime-stability.yml",
    ".github/workflows/runtime-architecture.yml",
    ".github/workflows/candidate-package.yml",
    ".github/workflows/codeql.yml",
    ".github/workflows/dependency-review.yml",
    ".github/workflows/pages.yml",
    ".github/workflows/release-package.yml",
    "docs/LICENSING.md",
    "docs/LICENSE_TRANSITION_RECORD_2026-07-15.md",
    "docs/EXTERNAL_IP_AND_PROVENANCE_REVIEW_2026-07-15.md",
    "docs/WORDING_AND_CLAIM_REVIEW_2026-07-15.md",
    "docs/architecture/STREAM_RUNTIME.md",
    "docs/architecture/RUNTIME_SNAPSHOT_AND_REPLAY.md",
    "docs/validation/TESTED_CONFIGURATIONS.md",
    "docs/validation/V1.3.0_BETA2_FIELD_EVIDENCE.md",
    "docs/development/RELEASE_CHECKLIST.md",
    "src/ProcessBus.Iec61850.Raw/Runtime/SvRuntimeSnapshot.cs",
    "src/ProcessBus.Iec61850.Raw/Runtime/AnalyzerRuntimeSnapshotSource.cs",
    "src/ProcessBus.Iec61850.Raw/Replay/PcapReplayReader.cs",
    "src/ProcessBus.Iec61850.Raw/Replay/ProcessBusReplaySession.cs",
    "tests/ProcessBus.Tests/RuntimeStabilityTests.cs",
    "tests/ProcessBus.Tests/PcapReplayRuntimeTests.cs",
    "tests/ProcessBus.Tests/PcapFormatVariantTests.cs",
    "tests/ProcessBus.Tests/AnalyzerRuntimeSnapshotSourceTests.cs"
)
$required | ForEach-Object { Assert-Path $_ }

if (Test-Path (Join-Path $repoRoot "LICENSE-APACHE-2.0")) {
    throw "Historical Apache license must not be present as an active root license on the current GPL branch."
}

$forbiddenPatterns = @(
    '(^|/)(bin|obj|artifacts|TestResults|captures|logs|temp|tmp)(/|$)',
    '(^|/)ProcessBusAnalyzer\.RnD\.sln$',
    '(^|/)DigSubAnalyzer/DigSubAnalyzer(/|$)',
    '(^|/)TOP_GLOBAL_UPGRADE\.md$',
    '(^|/)repair-.*\.ps1$',
    '(^|/)scripts/push-top-global\.ps1$',
    '^index\.html$',
    '^\.nojekyll$',
    '\.(pcap|pcapng|etl|exe|dll|pdb|log)$'
)

$paths = @()
if (Test-Path (Join-Path $repoRoot ".git")) {
    $paths = @(git ls-files --cached --others --exclude-standard | Where-Object {
        if ([string]::IsNullOrWhiteSpace($_)) { return $false }
        Test-Path -LiteralPath (Join-Path $repoRoot $_)
    })
    if ($LASTEXITCODE -ne 0) { throw "git ls-files failed." }
} else {
    $paths = @(Get-ChildItem -Path $repoRoot -Recurse -File | ForEach-Object {
        $_.FullName.Substring($repoRoot.Path.Length + 1).Replace('\', '/')
    })
}

foreach ($path in $paths) {
    $normalized = $path.Replace('\', '/')
    foreach ($pattern in $forbiddenPatterns) {
        if ($normalized -match $pattern) {
            throw "Repository hygiene violation: $normalized"
        }
    }
}

$license = Read-RepoText "LICENSE"
if ($license -notmatch 'GNU GENERAL PUBLIC LICENSE' -or $license -notmatch 'Version 3, 29 June 2007') {
    throw "LICENSE is not the GNU General Public License version 3 text."
}
if ($license -match 'Apache License') {
    throw "Current root LICENSE contains stale Apache license wording."
}

[xml]$props = Read-RepoText "Directory.Build.props"
$licenseExpression = $props.SelectSingleNode("/Project/PropertyGroup/PackageLicenseExpression")
if ($null -eq $licenseExpression -or $licenseExpression.InnerText.Trim() -ne 'GPL-3.0-or-later') {
    throw "Directory.Build.props must declare GPL-3.0-or-later."
}

$commercial = Read-RepoText "COMMERCIAL-LICENSE.md"
if ($commercial -notmatch 'not itself a commercial license' -or $commercial -notmatch 'grants no additional rights') {
    throw "Commercial notice must state that it is not itself a license and grants no additional rights."
}

$licensing = Read-RepoText "docs/LICENSING.md"
foreach ($marker in @(
    'GPL-3.0-or-later',
    '85d43a0fe58a5888a9e8008c168ab76d2333ea87',
    'archive/apache-2.0-final'
)) {
    if ($licensing -notmatch [regex]::Escape($marker)) {
        throw "docs/LICENSING.md is missing required marker: $marker"
    }
}

$readme = Read-RepoText "README.md"
if ($readme -notmatch 'GPL-3.0-or-later' -or $readme -notmatch 'COMMERCIAL-LICENSE\.md') {
    throw "README does not clearly state the GPL community and commercial licensing model."
}
if ($readme -match 'Source code is licensed under \*\*Apache-2\.0\*\*') {
    throw "README still presents Apache-2.0 as the current license."
}

$site = Read-RepoText "docs/index.html"
if ($site -notmatch 'https://spdx\.org/licenses/GPL-3\.0-or-later\.html') {
    throw "Landing-page structured data does not declare GPL-3.0-or-later."
}
if ($site -match 'https://www\.apache\.org/licenses/LICENSE-2\.0') {
    throw "Landing page still declares Apache-2.0 as the active structured license."
}
if ($site -notmatch 'commercial agreement' -or $site -notmatch 'grants no additional rights') {
    throw "Landing page does not explain the separate commercial path."
}

$prefixNode = $props.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
$suffixNode = $props.SelectSingleNode("/Project/PropertyGroup/VersionSuffix")
if ($null -eq $prefixNode) { throw "VersionPrefix is missing." }
$prefix = $prefixNode.InnerText.Trim()
$suffix = if ($null -eq $suffixNode) { "" } else { $suffixNode.InnerText.Trim() }
if ([string]::IsNullOrWhiteSpace($prefix)) { throw "VersionPrefix is empty." }
$version = if ([string]::IsNullOrWhiteSpace($suffix)) { $prefix } else { "$prefix-$suffix" }
Write-Host "Resolved repository version: $version"

if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion) -and $ExpectedVersion -ne $version) {
    throw "Version mismatch. Repository=$version, requested=$ExpectedVersion"
}

$releaseWorkflow = Read-RepoText ".github/workflows/release-package.yml"
if ($releaseWorkflow -notmatch [regex]::Escape($version)) {
    throw "Release workflow does not reference repository version $version."
}
if ($site -notmatch ('"softwareVersion"\s*:\s*"' + [regex]::Escape($version) + '"')) {
    throw "docs/index.html softwareVersion is not $version."
}
if ($readme -notmatch [regex]::Escape("ProcessBusInsight-v$version-win-x64-portable.zip")) {
    throw "README package example is not synchronized with version $version."
}
if ($readme -notmatch 'Category=RuntimeArchitecture') {
    throw "README does not document the RuntimeArchitecture validation filter."
}

$releaseNotesPath = "docs/RELEASE_NOTES_v$version.md"
Assert-Path $releaseNotesPath

$solution = Read-RepoText "ProcessBusSuite.sln"
if ($solution -notmatch 'ProcessBus.Tests') {
    throw "ProcessBus.Tests is not included in ProcessBusSuite.sln."
}

Write-Host "Repository health: PASS" -ForegroundColor Green
Write-Host "Version: $version"
Write-Host "Current community license: GPL-3.0-or-later"
Write-Host "Historical Apache boundary: 85d43a0fe58a5888a9e8008c168ab76d2333ea87"
Write-Host "Tracked/inspected files: $($paths.Count)"
