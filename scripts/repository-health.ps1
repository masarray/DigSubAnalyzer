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

$required = @(
    "ProcessBusSuite.sln",
    "Directory.Build.props",
    "README.md",
    "LICENSE",
    "SECURITY.md",
    "CONTRIBUTING.md",
    "CODE_OF_CONDUCT.md",
    "SUPPORT.md",
    "CHANGELOG.md",
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    ".github/CODEOWNERS",
    ".github/workflows/ci.yml",
    ".github/workflows/codeql.yml",
    ".github/workflows/dependency-review.yml",
    ".github/workflows/pages.yml",
    ".github/workflows/release-package.yml",
    "docs/architecture/STREAM_RUNTIME.md",
    "docs/validation/TESTED_CONFIGURATIONS.md",
    "docs/development/RELEASE_CHECKLIST.md"
)
$required | ForEach-Object { Assert-Path $_ }

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
    # Inspect the repository state that actually exists on disk. Plain
    # `git ls-files` also returns tracked files that have just been deleted
    # by the synchronization step, and it omits newly added untracked files.
    # Both behaviours produce false hygiene results during a clean upgrade.
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

[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot "Directory.Build.props") -Raw
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

$releaseWorkflow = Get-Content (Join-Path $repoRoot ".github/workflows/release-package.yml") -Raw
if ($releaseWorkflow -notmatch [regex]::Escape($version)) {
    throw "Release workflow does not reference repository version $version."
}

$site = Get-Content (Join-Path $repoRoot "docs/index.html") -Raw
if ($site -notmatch ('"softwareVersion"\s*:\s*"' + [regex]::Escape($version) + '"')) {
    throw "docs/index.html softwareVersion is not $version."
}

$readme = Get-Content (Join-Path $repoRoot "README.md") -Raw
if ($readme -notmatch [regex]::Escape("ProcessBusInsight-v$version-win-x64-portable.zip")) {
    throw "README package example is not synchronized with version $version."
}

$solution = Get-Content (Join-Path $repoRoot "ProcessBusSuite.sln") -Raw
if ($solution -notmatch 'ProcessBus.Tests') {
    throw "ProcessBus.Tests is not included in ProcessBusSuite.sln."
}

Write-Host "Repository health: PASS" -ForegroundColor Green
Write-Host "Version: $version"
Write-Host "Tracked/inspected files: $($paths.Count)"
