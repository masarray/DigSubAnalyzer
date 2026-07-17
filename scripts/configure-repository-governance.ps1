[CmdletBinding()]
param(
    [string] $Repository = 'masarray/DigSubAnalyzer',
    [switch] $Apply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$boundaryCommit = '85d43a0fe58a5888a9e8008c168ab76d2333ea87'
$boundaryTag = 'license-boundary/apache-2.0-final'
$requiredChecks = @(
    'Build, test, documents, and repository health',
    'CLA affirmation and DCO sign-off',
    'Analyze C#',
    'NuGet vulnerability audit',
    'GitHub dependency delta review',
    'Validate website, licensing, and generated documents'
)

function Invoke-GhJson {
    param(
        [Parameter(Mandatory)][string[]] $Arguments,
        [string] $InputJson
    )

    if (-not $Apply) {
        Write-Host "PREVIEW: gh $($Arguments -join ' ')"
        if ($InputJson) { Write-Host $InputJson }
        return
    }

    if ($InputJson) {
        $InputJson | gh @Arguments --input -
    }
    else {
        gh @Arguments
    }
    if ($LASTEXITCODE -ne 0) { throw "GitHub CLI command failed: gh $($Arguments -join ' ')" }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required.'
}

Write-Host "Repository governance target: $Repository"
Write-Host "Mode: $(if ($Apply) { 'APPLY' } else { 'PREVIEW' })"

if ($Apply) {
    git fetch origin $boundaryCommit
    if ($LASTEXITCODE -ne 0) { throw 'Could not fetch the historical boundary commit.' }
    if (-not (git tag --list $boundaryTag)) {
        git tag -a $boundaryTag $boundaryCommit -m 'Final active Apache-2.0 revision'
        git push origin $boundaryTag
        if ($LASTEXITCODE -ne 0) { throw 'Could not push the historical boundary tag.' }
    }
}
else {
    Write-Host "PREVIEW: create annotated tag $boundaryTag at $boundaryCommit if it does not already exist"
}

$branchPayload = [ordered]@{
    required_status_checks = [ordered]@{
        strict = $true
        contexts = $requiredChecks
    }
    enforce_admins = $true
    required_pull_request_reviews = [ordered]@{
        dismiss_stale_reviews = $true
        require_code_owner_reviews = $true
        required_approving_review_count = 1
        require_last_push_approval = $false
    }
    restrictions = $null
    required_linear_history = $true
    allow_force_pushes = $false
    allow_deletions = $false
    block_creations = $false
    required_conversation_resolution = $true
    lock_branch = $false
    allow_fork_syncing = $true
} | ConvertTo-Json -Depth 10 -Compress

Invoke-GhJson -Arguments @(
    'api',
    '--method', 'PUT',
    "repos/$Repository/branches/main/protection",
    '-H', 'Accept: application/vnd.github+json',
    '-H', 'X-GitHub-Api-Version: 2022-11-28'
) -InputJson $branchPayload

$rulesetPayload = [ordered]@{
    name = 'Protect license boundary tag'
    target = 'tag'
    enforcement = 'active'
    conditions = [ordered]@{
        ref_name = [ordered]@{
            include = @("refs/tags/$boundaryTag")
            exclude = @()
        }
    }
    rules = @(
        [ordered]@{ type = 'deletion' },
        [ordered]@{ type = 'non_fast_forward' }
    )
    bypass_actors = @()
} | ConvertTo-Json -Depth 10 -Compress

Invoke-GhJson -Arguments @(
    'api',
    '--method', 'POST',
    "repos/$Repository/rulesets",
    '-H', 'Accept: application/vnd.github+json',
    '-H', 'X-GitHub-Api-Version: 2022-11-28'
) -InputJson $rulesetPayload

Write-Host ''
Write-Host 'After applying:'
Write-Host '1. Confirm main requires the listed status checks.'
Write-Host "2. Confirm tag $boundaryTag resolves to $boundaryCommit."
Write-Host '3. Confirm tag deletion and non-fast-forward updates are blocked.'
Write-Host '4. Configure WINDOWS_SIGNING_CERTIFICATE_BASE64 and WINDOWS_SIGNING_CERTIFICATE_PASSWORD before publishing a public release.'
