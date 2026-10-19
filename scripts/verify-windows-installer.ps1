param(
    [Parameter(Mandatory = $true)]
    [string]$Installer,
    [string]$StageDir = '',
    [switch]$SkipInstallSmoke
)

$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
if ((Get-Item -LiteralPath $installerPath).Length -lt 1024 * 1024) {
    throw "Inno installer is too small: $installerPath"
}
if ([System.IO.Path]::GetExtension($installerPath) -ne '.exe' -or
    (Split-Path -Leaf $installerPath) -notmatch '^ProcessBusInsight-v.+-win-x64-setup\.exe$') {
    throw 'Unexpected installer name or extension.'
}
$installerDirectory = Split-Path $installerPath -Parent
$sumPath = Join-Path $installerDirectory 'SHA256SUMS.txt'
$sha = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
if (-not (Test-Path -LiteralPath $sumPath)) { throw "Missing installer checksums: $sumPath" }
$expectedLine = "$sha  $(Split-Path $installerPath -Leaf)"
if (-not (@(Get-Content $sumPath) -ccontains $expectedLine)) {
    throw "Installer checksum mismatch in SHA256SUMS.txt"
}

Write-Host "Verified installer checksum: $sha"
if ($SkipInstallSmoke) { return }

# Full install/repair/uninstall smoke test on an ephemeral GitHub Windows runner:
# no production apps, user settings or real capture devices are touched.
$root = Join-Path $env:TEMP ("ProcessBusInnoSmoke_" + [Guid]::NewGuid().ToString('N'))
$installDir = Join-Path $root 'installed'
$logDir = Join-Path $root 'logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$uninstallPath = Join-Path $installDir 'unins000.exe'
try {
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        $log = Join-Path $logDir "setup-$attempt.log"
        $arguments = @(
            '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
            ('/DIR="' + $installDir + '"'), ('/LOG="' + $log + '"')
        )
        $process = Start-Process -FilePath $installerPath -ArgumentList $arguments -PassThru -Wait
        if ($process.ExitCode -ne 0) {
            throw "Silent install/repair attempt $attempt failed: exit $($process.ExitCode). Log: $log"
        }
        $required = @(
            'ProcessBusInsight.exe',
            'Quick Start.pdf',
            'User Manual.pdf',
            'LICENSE.txt',
            'README.txt',
            'Bench/ProcessBus.InteropCli.exe',
            'unins000.exe'
        )
        foreach ($relative in $required) {
            if (-not (Test-Path -LiteralPath (Join-Path $installDir $relative) -PathType Leaf)) {
                throw "Installer smoke test: missing installed $relative."
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($StageDir)) {
            foreach ($relative in @('ProcessBusInsight.exe', 'User Manual.pdf', 'Quick Start.pdf')) {
                $source = Join-Path $StageDir $relative
                if (-not (Test-Path -LiteralPath $source)) {
                    throw "Smoke test source missing: $source"
                }
                $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
                $destHash = (Get-FileHash -LiteralPath (Join-Path $installDir $relative) -Algorithm SHA256).Hash
                if ($sourceHash -ne $destHash) { throw "Installed $relative does not match verified stage." }
            }
        }
        Write-Host "Installed payload and offline manuals verified (run $attempt / 2)."
    }

    $uninstallArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
        ('/LOG="' + (Join-Path $logDir 'uninstall.log') + '"'))
    $process = Start-Process -FilePath $uninstallPath -ArgumentList $uninstallArguments -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Silent uninstall failed: exit $($process.ExitCode)." }
    if (Test-Path -LiteralPath (Join-Path $installDir 'ProcessBusInsight.exe')) {
        throw 'Uninstaller left the installed main executable behind.'
    }
    Write-Host 'Installer verification passed: install, same-version repair, offline PDFs, uninstall.'
}
finally {
    # Only the uniquely generated ephemeral smoke path is removed.
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}
