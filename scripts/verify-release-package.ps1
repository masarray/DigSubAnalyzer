param(
    [Parameter(Mandatory=$true)]
    [string]$PackageZip,
    [string]$AppName = "ProcessBusInsight"
)

$ErrorActionPreference = "Stop"
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
        "Licensing.md"
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
    foreach ($marker in @('GPL-3.0-or-later', '85d43a0fe58a5888a9e8008c168ab76d2333ea87', 'archive/apache-2.0-final')) {
        if ($licensing -notmatch [regex]::Escape($marker)) {
            throw "Package verification failed. Licensing.md is missing: $marker"
        }
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

    $size = (Get-Item (Join-Path $temp "$AppName.exe")).Length
    if ($size -lt 1024) { throw "Package verification failed. Executable looks too small: $size bytes" }

    foreach ($pdfName in @("Quick Start.pdf", "User Manual.pdf")) {
        if ((Get-Item (Join-Path $temp $pdfName)).Length -lt 2048) {
            throw "Package verification failed. PDF document looks too small: $pdfName"
        }
    }

    Write-Host "Package verification passed: $PackageZip"
    Write-Host "Community license: GPL-3.0-or-later"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
