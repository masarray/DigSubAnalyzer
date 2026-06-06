<#
.SYNOPSIS
Configure this repository to publish GitHub Pages using GitHub Actions.

.DESCRIPTION
This script is intentionally small and safe. It only changes the GitHub Pages
publishing mode to workflow-based deployment, so .github/workflows/pages.yml
becomes the deployment source. It does not push code and does not delete branches.

.REQUIREMENTS
- GitHub CLI installed: https://cli.github.com/
- gh auth login already completed
- Admin/maintainer permission on the repository
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Owner = "masarray",
    [string]$Repo = "DigSubAnalyzer",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

function Assert-Gh {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        throw "GitHub CLI 'gh' is not installed or not available in PATH."
    }

    gh auth status 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not authenticated. Run: gh auth login"
    }
}

Assert-Gh

$repoFullName = "$Owner/$Repo"
Write-Host "Repository : $repoFullName"
Write-Host "Target     : GitHub Pages build_type=workflow"
Write-Host "Workflow   : .github/workflows/pages.yml"
Write-Host "Branch     : main"
Write-Host ""

Write-Host "Current Pages configuration, if available:"
try {
    gh api "repos/$Owner/$Repo/pages" --jq '{build_type: .build_type, html_url: .html_url, source: .source, status: .status}'
} catch {
    Write-Host "Pages site is not configured yet or current token cannot read the Pages settings."
}
Write-Host ""

if (-not $Apply) {
    Write-Host "Preview only. Re-run with -Apply to switch Pages to GitHub Actions."
    Write-Host "Manual UI equivalent: Settings -> Pages -> Build and deployment -> Source -> GitHub Actions"
    exit 0
}

if ($PSCmdlet.ShouldProcess($repoFullName, "Set GitHub Pages build_type to workflow")) {
    try {
        gh api --method PUT "repos/$Owner/$Repo/pages" `
            -H "Accept: application/vnd.github+json" `
            -f build_type=workflow `
            -F https_enforced=true
    } catch {
        Write-Host "PUT failed. Trying to create Pages configuration with workflow build type..."
        gh api --method POST "repos/$Owner/$Repo/pages" `
            -H "Accept: application/vnd.github+json" `
            -f build_type=workflow
    }

    Write-Host ""
    Write-Host "Updated Pages configuration:"
    gh api "repos/$Owner/$Repo/pages" --jq '{build_type: .build_type, html_url: .html_url, source: .source, status: .status}'
    Write-Host ""
    Write-Host "Now run: gh workflow run pages.yml --ref main"
}
