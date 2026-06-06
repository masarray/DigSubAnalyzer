<#
.SYNOPSIS
Show the repo branch, Pages configuration, and recent Pages workflow runs.
#>
[CmdletBinding()]
param(
    [string]$Owner = "masarray",
    [string]$Repo = "DigSubAnalyzer"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' is not installed or not available in PATH."
}

gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated. Run: gh auth login"
}

Write-Host "== Repository =="
gh repo view "$Owner/$Repo" --json nameWithOwner,defaultBranchRef,homepageUrl,url --jq '{repo: .nameWithOwner, default_branch: .defaultBranchRef.name, homepage: .homepageUrl, url: .url}'

Write-Host ""
Write-Host "== Pages configuration =="
try {
    gh api "repos/$Owner/$Repo/pages" --jq '{build_type: .build_type, html_url: .html_url, source: .source, status: .status, cname: .cname, https_enforced: .https_enforced}'
} catch {
    Write-Host "Unable to read Pages configuration. Check repository admin permission."
}

Write-Host ""
Write-Host "== Recent Pages workflow runs =="
gh run list --repo "$Owner/$Repo" --workflow pages.yml --limit 5

Write-Host ""
Write-Host "== Public URL check =="
$url = "https://$Owner.github.io/$Repo/"
try {
    $response = Invoke-WebRequest -Uri $url -Method Head -MaximumRedirection 5 -TimeoutSec 20
    Write-Host "$url -> HTTP $($response.StatusCode)"
} catch {
    Write-Host "$url -> request failed: $($_.Exception.Message)"
}
