# Camfrog Multi-ID portable bundle setup.
# Downloads Sandboxie-Plus (latest GitHub release) and the latest
# CamfrogMultiID release asset, verifies checksums where available,
# and prints the manual Camfrog client step (third-party installer).
# Usage: .\bundle-setup.ps1 [-OutDir .\bundle] [-ManagerVersion v1.0.0] [-InstallSandboxie]

param(
    [string]$OutDir = '',
    [string]$ManagerVersion = 'latest',
    [switch]$InstallSandboxie
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') { throw 'Run this bundle script on Windows.' }
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $PSScriptRoot 'bundle' }

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$bundleTools = Join-Path $OutDir 'tools'
New-Item -ItemType Directory -Path $bundleTools -Force | Out-Null
Set-Location -LiteralPath $PSScriptRoot

function Get-LatestSandboxieAsset {
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/sandboxie-plus/Sandboxie/releases/latest' -TimeoutSec 60
    $asset = $release.assets | Where-Object { $_.name -match '^Sandboxie-Plus-x64-v[\d\.]+\.exe$' } | Select-Object -First 1
    if ($null -eq $asset) { throw 'No Sandboxie-Plus x64 installer found in the latest release.' }
    return @{ Version = $release.tag_name; Url = $asset.browser_download_url; Name = $asset.name }
}

function Get-ManagerAsset([string]$version) {
    if ($version -eq 'latest') {
        $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/cvsz/zcamfrog/releases/latest' -TimeoutSec 60
    }
    else {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/cvsz/zcamfrog/releases/tags/$version" -TimeoutSec 60
    }
    $zip = $release.assets | Where-Object { $_.name -match '^CamfrogMultiID-v.*-win-x64\.zip$' } | Select-Object -First 1
    $sha = $release.assets | Where-Object { $_.name -match '\.sha256$' } | Select-Object -First 1
    if ($null -eq $zip) { throw 'No CamfrogMultiID win-x64 release asset found.' }
    return @{ ZipUrl = $zip.browser_download_url; ZipName = $zip.name; ShaUrl = if ($sha) { $sha.browser_download_url } else { $null } }
}

function Save-Verified([string]$url, [string]$dest) {
    Write-Host "Downloading $url ..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $url -OutFile $dest -TimeoutSec 600
}

Write-Host '== Camfrog Multi-ID bundle setup ==' -ForegroundColor Cyan

$sb = Get-LatestSandboxieAsset
Write-Host "Sandboxie-Plus $($sb.Version)" -ForegroundColor Green
$installerPath = Join-Path $bundleTools $sb.Name
Save-Verified $sb.Url $installerPath
Write-Host "Saved: $($sb.Name)"
if ($InstallSandboxie) {
    if (-not (Test-Elevated)) { throw 'Re-run with -InstallSandboxie from an elevated prompt (driver install requires admin).' }
    Write-Host 'Installing Sandboxie-Plus silently...' -ForegroundColor Yellow
    Start-Process -LiteralPath $installerPath -ArgumentList '/S' -Wait
    Write-Host 'Sandboxie-Plus installed. Settings auto-detects Start.exe.' -ForegroundColor Green
}
else {
    Write-Host 'Install it with default options, then point Settings at Start.exe (auto-detected).'
}

$mgr = Get-ManagerAsset $ManagerVersion
Write-Host "Manager $($mgr.ZipName)" -ForegroundColor Green
$zipPath = Join-Path $OutDir $mgr.ZipName
Save-Verified $mgr.ZipUrl $zipPath
if ($mgr.ShaUrl) {
    $shaPath = "$zipPath.sha256"
    Save-Verified $mgr.ShaUrl $shaPath
    $expected = (Get-Content -LiteralPath $shaPath -TotalCount 1).Split()[0].ToLowerInvariant()
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
    if ($expected -ne $actual) { throw "Checksum mismatch for $($mgr.ZipName)." }
    Write-Host 'Manager checksum OK.' -ForegroundColor Green
}
Expand-Archive -LiteralPath $zipPath -DestinationPath (Join-Path $OutDir 'CamfrogMultiID') -Force
Write-Host "Manager extracted to $(Join-Path $OutDir 'CamfrogMultiID')" -ForegroundColor Green

Write-Host ''
Write-Host 'Manual step (third-party installer, cannot be bundled):' -ForegroundColor Yellow
Write-Host '  1. Install Camfrog Video Chat from https://www.camfrog.com/'
Write-Host '  2. Start CamfrogMultiID.exe, open Settings, select the client executable.'
Write-Host '  3. Enable Sandboxie, press "Create Boxes For All Accounts", add Room URLs.'
Write-Host ''
Write-Host 'BUNDLE READY' -ForegroundColor Green
Write-Host "Bundle directory: $OutDir"
