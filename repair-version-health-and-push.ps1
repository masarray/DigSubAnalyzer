$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path $PSScriptRoot).Path
$healthPath = Join-Path $repoRoot "scripts\repository-health.ps1"
$pushPath = Join-Path $repoRoot "scripts\push-top-global.ps1"

if (-not (Test-Path -LiteralPath $healthPath)) { throw "Missing: $healthPath" }
if (-not (Test-Path -LiteralPath $pushPath)) { throw "Missing: $pushPath" }

$health = Get-Content -LiteralPath $healthPath -Raw
$old = @'
[xml]$props = Get-Content (Join-Path $repoRoot "Directory.Build.props")
$group = $props.Project.PropertyGroup | Select-Object -First 1
$prefix = [string]$group.VersionPrefix
$suffix = [string]$group.VersionSuffix
if ([string]::IsNullOrWhiteSpace($prefix)) { throw "VersionPrefix is missing." }
$version = if ([string]::IsNullOrWhiteSpace($suffix)) { $prefix } else { "$prefix-$suffix" }
'@
$new = @'
[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot "Directory.Build.props") -Raw
$prefixNode = $props.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
$suffixNode = $props.SelectSingleNode("/Project/PropertyGroup/VersionSuffix")

if ($null -eq $prefixNode) { throw "VersionPrefix is missing." }
$prefix = $prefixNode.InnerText.Trim()
$suffix = if ($null -eq $suffixNode) { "" } else { $suffixNode.InnerText.Trim() }

if ([string]::IsNullOrWhiteSpace($prefix)) { throw "VersionPrefix is empty." }
$version = if ([string]::IsNullOrWhiteSpace($suffix)) { $prefix } else { "$prefix-$suffix" }
Write-Host "Resolved repository version: $version"
'@

if ($health.Contains($old)) {
    $health = $health.Replace($old, $new)
    Set-Content -LiteralPath $healthPath -Value $health -Encoding UTF8
    Write-Host "Patched XML version parsing in repository-health.ps1" -ForegroundColor Green
}
elseif ($health.Contains('$prefixNode = $props.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")')) {
    Write-Host "XML version parsing is already fixed." -ForegroundColor Yellow
}
else {
    throw "Expected version parsing block was not found. Refusing an unsafe blind edit."
}

Set-ExecutionPolicy -Scope Process Bypass -Force
& $pushPath
