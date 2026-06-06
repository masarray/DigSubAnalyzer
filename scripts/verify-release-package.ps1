param(
    [Parameter(Mandatory=$true)]
    [string]$PackageZip,
    [string]$AppName = "ProcessBusInsight"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PackageZip)) {
    throw "Package ZIP not found: $PackageZip"
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("ProcessBusInsightVerify_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null

try {
    Expand-Archive -Path $PackageZip -DestinationPath $temp -Force

    $required = @(
        "$AppName.exe",
        "Quick Start.pdf",
        "User Manual.pdf",
        "README.txt",
        "LICENSE.txt"
    )

    foreach ($relative in $required) {
        $path = Join-Path $temp $relative
        if (-not (Test-Path $path)) {
            throw "Package verification failed. Missing: $relative"
        }
    }

    $batFiles = Get-ChildItem -Path $temp -Recurse -File -Filter "*.bat"
    if ($batFiles.Count -gt 0) {
        $names = ($batFiles | ForEach-Object { $_.FullName.Substring($temp.Length + 1) }) -join ', '
        throw "Package verification failed. Batch files are not allowed in the portable package: $names"
    }

    $exeFiles = Get-ChildItem -Path $temp -File -Filter "*.exe"
    if ($exeFiles.Count -ne 1 -or $exeFiles[0].Name -ne "$AppName.exe") {
        $names = ($exeFiles | Select-Object -ExpandProperty Name) -join ', '
        throw "Package verification failed. Expected exactly one root EXE: $AppName.exe. Found: $names"
    }

    $size = (Get-Item (Join-Path $temp "$AppName.exe")).Length
    if ($size -lt 1024) {
        throw "Package verification failed. Executable looks too small: $size bytes"
    }

    foreach ($pdfName in @("Quick Start.pdf", "User Manual.pdf")) {
        $pdfPath = Join-Path $temp $pdfName
        if ((Get-Item $pdfPath).Length -lt 2048) {
            throw "Package verification failed. PDF document looks too small: $pdfName"
        }
    }

    Write-Host "Package verification passed: $PackageZip"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
