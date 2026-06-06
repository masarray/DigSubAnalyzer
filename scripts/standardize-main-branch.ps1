<#
.SYNOPSIS
  Standardize this repository to a single public main branch.

.DESCRIPTION
  This helper prepares a clean public GitHub repository layout:
  - ensures a local main branch exists,
  - pushes main to origin,
  - sets GitHub default branch to main,
  - optionally deletes old remote branches after you confirm they are no longer needed.

  The script is intentionally conservative. By default it runs in preview mode.

.EXAMPLE
  .\scripts\standardize-main-branch.ps1 -Owner masarray -Repo DigSubAnalyzer -WhatIf

.EXAMPLE
  .\scripts\standardize-main-branch.ps1 -Owner masarray -Repo DigSubAnalyzer -Apply

.EXAMPLE
  .\scripts\standardize-main-branch.ps1 -Owner masarray -Repo DigSubAnalyzer -Apply -DeleteRemoteBranches master,public-repo-hardening
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$Owner,

  [Parameter(Mandatory = $true)]
  [string]$Repo,

  [string]$TargetBranch = "main",

  [string[]]$DeleteRemoteBranches = @(),

  [switch]$Apply,

  [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
  param(
    [Parameter(Mandatory = $true)] [string]$Description,
    [Parameter(Mandatory = $true)] [scriptblock]$Command
  )

  Write-Host "`n==> $Description" -ForegroundColor Cyan
  if (-not $Apply -or $WhatIf) {
    Write-Host "Preview only. Use -Apply to execute." -ForegroundColor Yellow
    return
  }

  & $Command
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
  throw "Git CLI was not found in PATH. Install Git for Windows first."
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  throw "GitHub CLI was not found in PATH. Install GitHub CLI first."
}

$authStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
  throw "GitHub CLI is not authenticated. Run: gh auth login"
}

$repoFullName = "$Owner/$Repo"
Write-Host "Repository : $repoFullName"
Write-Host "Target     : $TargetBranch"
Write-Host "Apply      : $Apply"
Write-Host "Delete     : $($DeleteRemoteBranches -join ', ')"

$currentBranch = (git branch --show-current).Trim()
if ([string]::IsNullOrWhiteSpace($currentBranch)) {
  throw "No current Git branch detected. Run this script from a normal local checkout."
}

Invoke-Step "Fetch origin" {
  git fetch origin --prune
}

Invoke-Step "Create or update local $TargetBranch from current branch '$currentBranch'" {
  $existing = git branch --list $TargetBranch
  if ($existing) {
    git checkout $TargetBranch
    git merge $currentBranch --ff-only
  } else {
    git branch -m $TargetBranch
  }
}

Invoke-Step "Push $TargetBranch to origin" {
  git push -u origin $TargetBranch
}

Invoke-Step "Set GitHub default branch to $TargetBranch" {
  gh repo edit $repoFullName --default-branch $TargetBranch --delete-branch-on-merge=true
}

foreach ($branch in $DeleteRemoteBranches) {
  if ($branch -eq $TargetBranch) {
    Write-Host "Skipping delete for target branch '$TargetBranch'." -ForegroundColor Yellow
    continue
  }

  Invoke-Step "Delete remote branch origin/$branch" {
    git push origin --delete $branch
  }
}

Write-Host "`nDone. Expected public branch layout: only '$TargetBranch' remains as the public default branch." -ForegroundColor Green
