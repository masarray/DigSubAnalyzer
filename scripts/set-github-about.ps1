param(
    [string]$Owner = "masarray",
    [string]$Repo = "DigSubAnalyzer",
    [string]$HomepageUrl = "https://masarray.github.io/DigSubAnalyzer/",
    [string]$Description = "Free open-source IEC 61850 Process Bus analyzer for SV, GOOSE, PTP, SCL validation, FAT/SAT troubleshooting, and commissioning evidence.",
    [string[]]$Topics = @(
        "iec61850",
        "sampled-values",
        "goose",
        "ptp",
        "digital-substation",
        "substation-automation",
        "process-bus",
        "wpf",
        "dotnet",
        "windows",
        "fat-sat",
        "commissioning"
    ),
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Assert-GhAvailable {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        throw "GitHub CLI 'gh' was not found. Install it from https://cli.github.com/ and login first."
    }

    gh auth status --hostname github.com *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not logged in. Run: gh auth login"
    }
}

Assert-GhAvailable

$repoSlug = "$Owner/$Repo"
$topicsCsv = ($Topics | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Select-Object -Unique) -join ","

Write-Host "Repository About preview"
Write-Host "  Repository : $repoSlug"
Write-Host "  Homepage   : $HomepageUrl"
Write-Host "  Description: $Description"
Write-Host "  Topics     : $topicsCsv"
Write-Host "  Issues     : enabled"
Write-Host "  Wiki       : disabled"
Write-Host "  Projects   : disabled"
Write-Host "  Discussions: disabled when supported by installed gh version"

if ($WhatIf) {
    Write-Host "WhatIf mode: no changes applied."
    exit 0
}

$editArgs = @(
    "repo", "edit", $repoSlug,
    "--description", $Description,
    "--homepage", $HomepageUrl,
    "--enable-issues=true",
    "--enable-wiki=false",
    "--enable-projects=false"
)

gh @editArgs
if ($LASTEXITCODE -ne 0) { throw "gh repo edit failed." }

if (-not [string]::IsNullOrWhiteSpace($topicsCsv)) {
    gh repo edit $repoSlug --add-topic $topicsCsv
    if ($LASTEXITCODE -ne 0) { throw "Failed to apply repository topics." }
}

# Some gh versions support --enable-discussions; older versions do not.
gh repo edit $repoSlug --enable-discussions=false *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Installed GitHub CLI version may not support --enable-discussions. Repository description, homepage, issues, wiki, projects, and topics were still applied."
}

Write-Host "Repository About panel updated."
