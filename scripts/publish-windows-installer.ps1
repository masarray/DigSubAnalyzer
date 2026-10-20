param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:\.\d+)?(?:-[A-Za-z0-9.-]+)?$')]
    [string]$Version,
    [string]$Runtime = 'win-x64',
    [string]$AppName = 'ProcessBusInsight',
    [string]$OutputRoot = 'artifacts',
    [string]$IsccPath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ($Runtime -ne 'win-x64') { throw 'Inno installer currently supports win-x64 only.' }

$stageDir = Join-Path $repoRoot "$OutputRoot/package/$AppName-v$Version-$Runtime-portable"
$releaseDir = Join-Path $repoRoot "$OutputRoot/release"
$issPath = Join-Path $repoRoot 'installer/ProcessBusInsight.iss'
$installerName = "$AppName-v$Version-$Runtime-setup.exe"
$installerPath = Join-Path $releaseDir $installerName
$shaFile = Join-Path $releaseDir 'SHA256SUMS.txt'

# Fail closed before producing an installer. This is the identical verified
# portable payload, including legal docs, PDFs and the separate offline bench CLI.
$mandatory = @(
    "$AppName.exe", 'Quick Start.pdf', 'User Manual.pdf',
    'LICENSE.txt', 'NOTICE.txt', 'COMMERCIAL-LICENSE.md',
    'COPYRIGHT.md', 'TRADEMARK.md', 'THIRD_PARTY_NOTICES.md',
    'Licensing.md', 'README.txt', 'Bench/ProcessBus.InteropCli.exe',
    'Bench/START_HERE.md'
)
foreach ($file in $mandatory) {
    $item = Join-Path $stageDir $file
    if (-not (Test-Path -LiteralPath $item -PathType Leaf)) {
        throw "Installer staging is missing $file. Run publish-windows-portable.ps1 first."
    }
}
if (-not (Test-Path -LiteralPath $issPath -PathType Leaf)) {
    throw "Missing Inno setup definition: $issPath"
}

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
    if ([string]::IsNullOrWhiteSpace($IsccPath)) {
        foreach ($candidate in @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        )) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $IsccPath = $candidate
                break
            }
        }
    }
}
if (-not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw 'Inno Setup 6 compiler (ISCC.exe) is unavailable. Install Inno Setup 6 first.'
}

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
# Avoid prerelease labels in Win32 VERSIONINFO numeric resource.
$numericVersion = $Version.Split('-', 2)[0]
$versionNumbers = $numericVersion.Split('.')
if ($versionNumbers.Length -eq 3) { $numericVersion += '.0' }

Write-Host "Compiling Inno Setup installer: $installerName"
Write-Host "Payload: $stageDir"
Write-Host "Inno compiler: $IsccPath"
$compilerArgs = @(
    "/DAppVersion=$Version", "/DFileVersion=$numericVersion",
    "/DStageDir=$stageDir", "/DOutputDir=$releaseDir", $issPath
)
& $IsccPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "ISCC succeeded without producing $installerPath."
}
if ((Get-Item -LiteralPath $installerPath).Length -lt 1024 * 1024) {
    throw 'Installer is unexpectedly small; refusing to publish.'
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = "$hash  $installerName"
if (Test-Path $shaFile) {
    # Running this step twice must not duplicate its checksum line.
    $others = @(Get-Content $shaFile | Where-Object {
        $_ -and $_ -notmatch ('\s+' + [regex]::Escape($installerName) + '$')
    })
    @($others + $checksumLine) | Set-Content $shaFile -Encoding ASCII
} else {
    Set-Content $shaFile $checksumLine -Encoding ASCII
}
Write-Host "Created $installerPath"
Write-Host "SHA256 $hash"
