# MSIX packaging for Camfrog Multi-ID Manager (path-ready, not yet verified).
# Requires: Windows SDK (makeappx.exe, signtool.exe) and a code-signing
# certificate (self-signed is fine for local install; Store/EV for distribution).
# Usage: .\package-msix.ps1 [-Version 1.0.0] [-CertificateThumbprint <tp>]
#
# Steps performed:
#   1. Verify makeappx/signtool exist (clear error otherwise).
#   2. Build + self-contained publish via build-release.ps1.
#   3. Generate solid-color logo assets (no designer art needed).
#   4. Write AppxManifest.xml from the layout below.
#   5. Pack with makeappx, sign with signtool (self-signed cert auto-created
#      for local use when no thumbprint is supplied).
# MSIX is NOT required for the portable zip release; that path stays primary.

param(
    [string]$Version = '1.0.0',
    [string]$CertificateThumbprint = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') { throw 'Run MSIX packaging on Windows.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be MAJOR.MINOR.PATCH, got: $Version" }

$makeappx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if ($null -eq $makeappx) { throw 'makeappx.exe not found. Install the Windows SDK (App Packaging tools) first.' }
if ($null -eq $signtool) { throw 'signtool.exe not found. Install the Windows SDK first.' }

$publish = Join-Path $PSScriptRoot 'src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish'
$msixRoot = Join-Path $PSScriptRoot 'msix'
$assets = Join-Path $msixRoot 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null

Write-Host 'Publishing self-contained win-x64...' -ForegroundColor Yellow
& (Join-Path $PSScriptRoot 'build-release.ps1')

Write-Host 'Generating logo assets...' -ForegroundColor Yellow
Add-Type -AssemblyName System.Drawing
$tiles = @(44, 50, 150, 310)
foreach ($size in $tiles) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    try {
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $gfx.Clear([System.Drawing.Color]::FromArgb(16, 110, 190))
            $font = New-Object System.Drawing.Font('Segoe UI', [float]($size / 3), [System.Drawing.FontStyle]::Bold)
            try {
                $brush = [System.Drawing.Brushes]::White
                $format = New-Object System.Drawing.StringFormat
                $format.Alignment = [System.Drawing.StringAlignment]::Center
                $format.LineAlignment = [System.Drawing.StringAlignment]::Center
                $gfx.DrawString('CF', $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $size, $size), $format)
            }
            finally { $font.Dispose() }
        }
        finally { $gfx.Dispose() }
        $bmp.Save((Join-Path $assets "Logo$size.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bmp.Dispose() }
}

$publisher = 'CN=CamfrogMultiID-Local'
if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    Write-Host 'Creating ephemeral self-signed certificate (local install only)...' -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
        -FriendlyName 'CamfrogMultiID MSIX (local)' -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    $CertificateThumbprint = $cert.Thumbprint
}

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
  <Identity Name="cvsz.CamfrogMultiID" Publisher="$publisher" Version="$Version.0" ProcessorArchitecture="x64" />
  <Properties><DisplayName>Camfrog Multi-ID Manager</DisplayName><PublisherDisplayName>cvsz</PublisherDisplayName><Logo>Assets\Logo50.png</Logo></Properties>
  <Resources><Resource Language="en-us" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
  <Applications><Application Id="CamfrogMultiID" Executable="CamfrogMultiID.exe" EntryPoint="Windows.FullTrustApplication"><uap:VisualElements DisplayName="Camfrog Multi-ID Manager" Description="Manage multiple Camfrog client identities." BackgroundColor="#106EBE" Square150x150Logo="Assets\Logo150.png" Square44x44Logo="Assets\Logo44.png" /></Application></Applications>
</Package>
"@
Set-Content -LiteralPath (Join-Path $msixRoot 'AppxManifest.xml') -Value $manifest -Encoding UTF8

Copy-Item -LiteralPath (Join-Path $publish 'CamfrogMultiID.exe') -Destination (Join-Path $msixRoot 'CamfrogMultiID.exe') -Force

$msix = Join-Path $PSScriptRoot "CamfrogMultiID-$Version-x64.msix"
& $makeappx pack /d $msixRoot /p $msix /nv
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }
& $signtool sign /fd SHA256 /a /sha1 $CertificateThumbprint /tr http://timestamp.digicert.com /td SHA256 $msix
if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE." }

Write-Host ''
Write-Host 'MSIX READY' -ForegroundColor Green
Write-Host "Package: $msix"
Write-Host 'Note: self-signed packages require trusting the certificate before install.'
