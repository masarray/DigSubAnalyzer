param(
    [string]$Repository = "https://github.com/masarray/DigSubAnalyzer.git",
    [string]$Branch = "hardening/v1.3.0-beta.1",
    [switch]$SkipBuild,
    [switch]$NoPullRequest
)

$ErrorActionPreference = "Stop"
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$workRoot = Join-Path $env:TEMP "DigSubAnalyzer-top-global-$timestamp"

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command was not found: $Name"
    }
}

Require-Command git
if (-not $SkipBuild) { Require-Command dotnet }

Write-Host "Source package: $sourceRoot"
Write-Host "Temporary clone: $workRoot"

git clone $Repository $workRoot
if ($LASTEXITCODE -ne 0) { throw "git clone failed." }

Push-Location $workRoot
try {
    git checkout main
    git pull --ff-only origin main

    git ls-remote --exit-code --heads origin $Branch *> $null
    if ($LASTEXITCODE -eq 0) {
        $Branch = "$Branch-$timestamp"
        Write-Host "Remote branch already exists. Using: $Branch" -ForegroundColor Yellow
    }

    git checkout -b $Branch

    $excludedDirectories = @(
        ".git", ".vs", "bin", "obj", "artifacts", "TestResults", "coverage",
        "captures", "logs", "temp", "tmp", ".dotnet", ".push-work"
    )
    $robocopyArgs = @($sourceRoot, $workRoot, "/MIR", "/COPY:DAT", "/DCOPY:DAT", "/R:2", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/XD") + $excludedDirectories
    & robocopy @robocopyArgs
    if ($LASTEXITCODE -gt 7) { throw "robocopy failed with exit code $LASTEXITCODE" }

    # Remove obsolete public-repo files explicitly. Robocopy /MIR deletes
    # them from disk, but Git still reports them as tracked deletions until
    # the index is refreshed. Staging here makes the validation input match
    # the exact tree that will be committed.
    $obsoletePaths = @(
        "ProcessBusAnalyzer.RnD.sln",
        "digsub-level-up-NOTES.md"
    )
    foreach ($obsoletePath in $obsoletePaths) {
        $fullObsoletePath = Join-Path $workRoot $obsoletePath
        if (Test-Path -LiteralPath $fullObsoletePath) {
            Remove-Item -LiteralPath $fullObsoletePath -Force -Recurse
        }
    }

    git add -A
    if ($LASTEXITCODE -ne 0) { throw "git add failed before repository validation." }

    .\scripts\repository-health.ps1

    if (-not $SkipBuild) {
        dotnet --info
        dotnet restore .\ProcessBusSuite.sln
        dotnet build .\ProcessBusSuite.sln -c Release --no-restore /p:ContinuousIntegrationBuild=true
        dotnet test .\ProcessBusSuite.sln -c Release --no-build --logger "trx;LogFileName=local-prepush.trx" --results-directory .\TestResults
    }

    git diff --check
    if ($LASTEXITCODE -ne 0) { throw "git diff --check failed." }

    git add -A
    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { throw "No changes were found to commit." }

    git status --short
    git commit -m "Harden repository for v1.3.0 beta"
    git push -u origin $Branch

    $compareUrl = "https://github.com/masarray/DigSubAnalyzer/compare/main...$Branch?expand=1"
    if (-not $NoPullRequest -and (Get-Command gh -ErrorAction SilentlyContinue)) {
        $body = @"
## Repository hardening

- synchronize public beta versioning and documentation
- add CI evidence, CodeQL, dependency review, and Dependabot
- add issue/PR templates, CODEOWNERS, support, conduct, and changelog
- add repository hygiene and version-consistency gates
- document stream-runtime invariants and release validation gates
- preserve the receive-only/raw-passive engineering boundary

## Local validation

- repository health gate passed
- Release build and tests passed before push
"@
        try {
            gh pr create --repo masarray/DigSubAnalyzer --base main --head $Branch --title "Harden repository for v1.3.0-beta.1" --body $body
        } catch {
            Write-Warning "Branch was pushed, but automatic PR creation failed. Open: $compareUrl"
        }
    } else {
        Write-Host "Open this URL to create the pull request:" -ForegroundColor Cyan
        Write-Host $compareUrl
    }

    Write-Host "Completed. Working clone retained at: $workRoot" -ForegroundColor Green
} finally {
    Pop-Location
}
