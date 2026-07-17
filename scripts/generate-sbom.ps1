[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function New-DeterministicUuidUrn {
    param([Parameter(Mandatory)][string] $Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
    }
    finally {
        $sha256.Dispose()
    }

    $uuidBytes = [byte[]]::new(16)
    [Array]::Copy($hash, $uuidBytes, $uuidBytes.Length)
    $uuidBytes[6] = [byte](($uuidBytes[6] -band 0x0F) -bor 0x50)
    $uuidBytes[8] = [byte](($uuidBytes[8] -band 0x3F) -bor 0x80)
    $hex = -join ($uuidBytes | ForEach-Object { $_.ToString('x2') })
    $uuid = '{0}-{1}-{2}-{3}-{4}' -f $hex.Substring(0,8), $hex.Substring(8,4), $hex.Substring(12,4), $hex.Substring(16,4), $hex.Substring(20,12)
    "urn:uuid:$uuid"
}

function Get-HighestRuntimeVersion {
    param([Parameter(Mandatory)][string] $RuntimeName)

    $lines = & dotnet --list-runtimes 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet --list-runtimes failed.`n$($lines -join [Environment]::NewLine)"
    }

    $versions = foreach ($line in $lines) {
        if ($line -match ('^' + [regex]::Escape($RuntimeName) + '\s+([^\s]+)\s+')) {
            try { [version] $Matches[1] } catch { }
        }
    }

    $selected = $versions | Where-Object { $_.Major -eq 8 } | Sort-Object -Descending | Select-Object -First 1
    if (-not $selected) {
        throw "Could not resolve an installed .NET 8 runtime for $RuntimeName."
    }

    $selected.ToString()
}

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $root 'artifacts\release\ProcessBusInsight-SBOM.cdx.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $root $OutputPath
}

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null

$sourceCommit = (& git -C $root rev-parse HEAD 2>&1) -join ''
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceCommit)) {
    throw 'Could not resolve source commit for SBOM.'
}
$sourceCommit = $sourceCommit.Trim()

$sourceTimestampText = (& git -C $root show -s --format=%cI HEAD 2>&1) -join ''
if ($LASTEXITCODE -ne 0) {
    throw 'Could not resolve source commit timestamp for SBOM.'
}
$sourceTimestamp = [DateTimeOffset]::Parse($sourceTimestampText.Trim(), [System.Globalization.CultureInfo]::InvariantCulture).ToUniversalTime().ToString('o')

$netRuntimeVersion = Get-HighestRuntimeVersion 'Microsoft.NETCore.App'
$windowsDesktopVersion = Get-HighestRuntimeVersion 'Microsoft.WindowsDesktop.App'
$serialNumber = New-DeterministicUuidUrn "https://github.com/masarray/DigSubAnalyzer|$Version|$sourceCommit"

$components = @(
    [ordered]@{
        type = 'framework'
        'bom-ref' = "pkg:generic/Microsoft.NETCore.App@$netRuntimeVersion"
        name = 'Microsoft.NETCore.App'
        version = $netRuntimeVersion
        licenses = @([ordered]@{ license = [ordered]@{ id = 'MIT' } })
        properties = @([ordered]@{ name = 'processbus:distribution'; value = 'included in self-contained publish' })
    },
    [ordered]@{
        type = 'framework'
        'bom-ref' = "pkg:generic/Microsoft.WindowsDesktop.App@$windowsDesktopVersion"
        name = 'Microsoft.WindowsDesktop.App'
        version = $windowsDesktopVersion
        licenses = @([ordered]@{ license = [ordered]@{ id = 'MIT' } })
        properties = @([ordered]@{ name = 'processbus:distribution'; value = 'included in self-contained WPF publish' })
    },
    [ordered]@{
        type = 'application'
        'bom-ref' = 'pkg:generic/Npcap@externally-installed'
        name = 'Npcap'
        version = 'externally-installed'
        licenses = @([ordered]@{ license = [ordered]@{ name = 'Npcap license'; url = 'https://npcap.com/oem/redist.html' } })
        properties = @([ordered]@{ name = 'processbus:distribution'; value = 'runtime prerequisite; not bundled' })
    }
)

$sbom = [ordered]@{
    bomFormat = 'CycloneDX'
    specVersion = '1.5'
    serialNumber = $serialNumber
    version = 1
    metadata = [ordered]@{
        timestamp = $sourceTimestamp
        tools = [ordered]@{
            components = @([ordered]@{
                type = 'application'
                author = 'Process Bus Insight project'
                name = 'generate-sbom.ps1'
                version = '1.0.0'
            })
        }
        component = [ordered]@{
            type = 'application'
            'bom-ref' = "pkg:generic/ProcessBusInsight@$Version"
            name = 'Process Bus Insight'
            version = $Version
            licenses = @([ordered]@{ license = [ordered]@{ id = 'GPL-3.0-or-later' } })
            properties = @(
                [ordered]@{ name = 'processbus:source-commit'; value = $sourceCommit },
                [ordered]@{ name = 'processbus:historical-license-boundary'; value = '85d43a0fe58a5888a9e8008c168ab76d2333ea87' },
                [ordered]@{ name = 'processbus:product-boundary'; value = 'receive-only' }
            )
        }
    }
    components = $components
}

$json = $sbom | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($OutputPath, $json + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "CycloneDX SBOM written: $OutputPath"
Write-Host "Source commit: $sourceCommit"
Write-Host "Serial number: $serialNumber"
Write-Host "Components: $($components.Count)"
