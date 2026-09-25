# Builds Marquee Reminder end to end: app, tests, installer. Results go to .\dist:
#   dist\MarqueeReminder-Setup.msi   the installer to share and run
#   dist\app\MarqueeReminder.exe     the app itself (portable copy, with its two support files)
#
# Usage (from this folder):  powershell -ExecutionPolicy Bypass -File build.ps1
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Dotnet {
    # Prefer a dotnet on PATH that has an SDK; fall back to a per-user SDK install.
    $onPath = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($onPath -and (& $onPath.Source --list-sdks)) { return $onPath.Source }
    $userSdk = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk\dotnet.exe'
    if (Test-Path $userSdk) { return $userSdk }
    throw 'No .NET SDK found. Install one from https://dotnet.microsoft.com/download (version 6 or later).'
}

function Invoke-Step([string]$name, [scriptblock]$action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) { throw "$name failed (exit code $LASTEXITCODE)." }
}

$dotnet = Find-Dotnet
$solution = Join-Path $root 'MarqueeReminder.sln'
$installer = Join-Path $root 'installer\MarqueeReminder.Installer.wixproj'

Invoke-Step 'Build app' { & $dotnet build $solution -c Release -nologo -v q }
if (-not $SkipTests) {
    Invoke-Step 'Run tests' { & $dotnet test $solution -c Release --no-build -nologo -v q }
}
Invoke-Step 'Build installer' { & $dotnet build $installer -c Release -nologo -v q }

$dist = Join-Path $root 'dist'
$distApp = Join-Path $dist 'app'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $distApp | Out-Null

Copy-Item (Join-Path $root 'installer\bin\Release\MarqueeReminder-Setup.msi') $dist
$appOutput = Join-Path $root 'src\MarqueeReminder\bin\Release\net48'
foreach ($file in 'MarqueeReminder.exe', 'MarqueeReminder.exe.config', 'MarqueeReminder.Core.dll') {
    Copy-Item (Join-Path $appOutput $file) $distApp
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host "  Installer: $(Join-Path $dist 'MarqueeReminder-Setup.msi')"
Write-Host "  App:       $(Join-Path $distApp 'MarqueeReminder.exe')"
