[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$PackageZip,
    [string]$AppName = "ProcessBusInsight",
    [string]$ExpectedVersion = "1.4.0-beta.2",
    [switch]$RequireAuthenticode
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
if (-not (Test-Path $PackageZip)) { throw "Package ZIP not found: $PackageZip" }

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("ProcessBusInsightVerify_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null

try {
    Expand-Archive -Path $PackageZip -DestinationPath $temp -Force

    $required = @(
        "$AppName.exe",
        "Quick Start.pdf",
        "User Manual.pdf",
        "README.txt",
        "LICENSE.txt",
        "NOTICE.txt",
        "COMMERCIAL-LICENSE.md",
        "COPYRIGHT.md",
        "TRADEMARK.md",
        "THIRD_PARTY_NOTICES.md",
        "Licensing.md",
        "Asset Provenance.md",
        "SOURCE.md",
        "sbom.cdx.json"
    )
    foreach ($relative in $required) {
        if (-not (Test-Path (Join-Path $temp $relative))) {
            throw "Package verification failed. Missing: $relative"
        }
    }

    if (Test-Path (Join-Path $temp "LICENSE-APACHE-2.0")) {
        throw "Package verification failed. Historical Apache license is presented as a current package license."
    }

    $license = Get-Content (Join-Path $temp "LICENSE.txt") -Raw
    if ($license -notmatch 'GNU GENERAL PUBLIC LICENSE' -or $license -notmatch 'Version 3, 29 June 2007') {
        throw "Package verification failed. LICENSE.txt is not GNU GPL version 3."
    }
    if ($license -match 'Apache License') {
        throw "Package verification failed. LICENSE.txt contains stale Apache wording."
    }

    $commercial = Get-Content (Join-Path $temp "COMMERCIAL-LICENSE.md") -Raw
    if ($commercial -notmatch 'not itself a commercial license' -or $commercial -notmatch 'grants no additional rights') {
        throw "Package verification failed. Commercial notice does not state its non-grant boundary."
    }

    $licensing = Get-Content (Join-Path $temp "Licensing.md") -Raw
    foreach ($marker in @('GPL-3.0-or-later', '85d43a0fe58a5888a9e8008c168ab76d2333ea87', 'archive/apache-2.0-final', 'SOURCE.md', 'sbom.cdx.json')) {
        if ($licensing -notmatch [regex]::Escape($marker)) {
            throw "Package verification failed. Licensing.md is missing: $marker"
        }
    }

    $source = Get-Content (Join-Path $temp "SOURCE.md") -Raw
    foreach ($marker in @('Built commit:', 'Source-head commit:', 'Tested merge commit:', 'Immutable source archive', 'GPL-3.0-or-later')) {
        if ($source -notmatch [regex]::Escape($marker)) {
            throw "Package verification failed. SOURCE.md is missing: $marker"
        }
    }
    if ($source -notmatch 'https://github\.com/masarray/DigSubAnalyzer/archive/[0-9a-f]{40}\.zip') {
        throw "Package verification failed. SOURCE.md does not contain an immutable commit archive URL."
    }

    $sbom = Get-Content (Join-Path $temp "sbom.cdx.json") -Raw | ConvertFrom-Json -Depth 100
    if ($sbom.bomFormat -ne 'CycloneDX' -or $sbom.specVersion -ne '1.5') {
        throw "Package verification failed. sbom.cdx.json is not CycloneDX 1.5."
    }
    if ($sbom.metadata.component.name -ne 'Process Bus Insight' -or $sbom.metadata.component.version -ne $ExpectedVersion) {
        throw "Package verification failed. SBOM product/version mismatch."
    }
    $sbomLicense = $sbom.metadata.component.licenses[0].license.id
    if ($sbomLicense -ne 'GPL-3.0-or-later') {
        throw "Package verification failed. SBOM does not identify GPL-3.0-or-later."
    }

    $batFiles = Get-ChildItem -Path $temp -Recurse -File -Filter "*.bat"
    if ($batFiles.Count -gt 0) {
        $names = ($batFiles | ForEach-Object { $_.FullName.Substring($temp.Length + 1) }) -join ', '
        throw "Package verification failed. Batch files are not allowed: $names"
    }

    $exeFiles = Get-ChildItem -Path $temp -File -Filter "*.exe"
    if ($exeFiles.Count -ne 1 -or $exeFiles[0].Name -ne "$AppName.exe") {
        $names = ($exeFiles | Select-Object -ExpandProperty Name) -join ', '
        throw "Package verification failed. Expected exactly one root EXE: $AppName.exe. Found: $names"
    }

    $exePath = Join-Path $temp "$AppName.exe"
    $size = (Get-Item $exePath).Length
    if ($size -lt 1024) { throw "Package verification failed. Executable looks too small: $size bytes" }

    $versionInfo = (Get-Item $exePath).VersionInfo
    if ($versionInfo.ProductVersion -notmatch [regex]::Escape($ExpectedVersion)) {
        throw "Package verification failed. EXE ProductVersion '$($versionInfo.ProductVersion)' does not contain $ExpectedVersion."
    }

    $signature = Get-AuthenticodeSignature -FilePath $exePath
    if ($RequireAuthenticode -and $signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "Package verification failed. A valid Authenticode signature is required; status=$($signature.Status)."
    }
    Write-Host "Authenticode status: $($signature.Status)"

    $binaryBytes = [System.IO.File]::ReadAllBytes($exePath)
    $binaryAscii = [System.Text.Encoding]::ASCII.GetString($binaryBytes)
    $binaryUnicode = [System.Text.Encoding]::Unicode.GetString($binaryBytes)
    $binaryText = $binaryAscii + "`n" + $binaryUnicode
    foreach ($forbidden in @('Source code is licensed under Apache-2.0', 'Text="Apache-2.0"', 'Version 1.0.0', 'Build 2026.04', 'oscilloscope-level')) {
        if ($binaryText.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Package verification failed. EXE contains stale public wording: $forbidden"
        }
    }
    foreach ($requiredBinaryMarker in @('GPL-3.0-or-later', 'Separate negotiated and executed agreement')) {
        if ($binaryText.IndexOf($requiredBinaryMarker, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Package verification failed. EXE is missing expected embedded wording: $requiredBinaryMarker"
        }
    }

    foreach ($pdfName in @("Quick Start.pdf", "User Manual.pdf")) {
        if ((Get-Item (Join-Path $temp $pdfName)).Length -lt 2048) {
            throw "Package verification failed. PDF document looks too small: $pdfName"
        }
    }

    $readme = Get-Content (Join-Path $temp "README.txt") -Raw
    foreach ($marker in @($ExpectedVersion, 'GPL-3.0-or-later', 'SOURCE.md', 'sbom.cdx.json')) {
        if ($readme -notmatch [regex]::Escape($marker)) {
            throw "Package verification failed. README.txt is missing: $marker"
        }
    }

    Write-Host "Package verification passed: $PackageZip"
    Write-Host "Community license: GPL-3.0-or-later"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
