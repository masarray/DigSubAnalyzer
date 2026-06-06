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
        "README_QUICK_START.txt",
        "LICENSE",
        "Start-ProcessBusInsight.bat",
        "app/$AppName.exe"
    )

    foreach ($relative in $required) {
        $path = Join-Path $temp $relative
        if (-not (Test-Path $path)) {
            throw "Package verification failed. Missing: $relative"
        }
    }


    $appFiles = Get-ChildItem -Path (Join-Path $temp "app") -File
    if ($appFiles.Count -ne 1 -or $appFiles[0].Name -ne "$AppName.exe") {
        $names = ($appFiles | Select-Object -ExpandProperty Name) -join ', '
        throw "Package verification failed. Single-file release expected exactly app/$AppName.exe. Found: $names"
    }

    $exe = Join-Path $temp "app/$AppName.exe"
    $size = (Get-Item $exe).Length
    if ($size -lt 1024) {
        throw "Package verification failed. Executable looks too small: $size bytes"
    }

    Write-Host "Package verification passed: $PackageZip"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
