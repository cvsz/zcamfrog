<#
.SYNOPSIS
    Verifies that a self-contained single-file publish contains exactly the
    expected files and nothing else.
.DESCRIPTION
    A single-file self-contained publish must collapse to the manager
    executable plus its symbol files. If a new dependency stops being bundled
    (or an old one starts shipping as a loose file), the artifact silently
    changes shape, so CI fails here instead of shipping a surprise.

    The allow-list is exact: unexpected files are an error, not a warning.
.EXAMPLE
    .\scripts\verify-publish.ps1 -PublishDir src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:OS -ne 'Windows_NT') { throw 'Run this WPF verification on Windows.' }

$exeName = 'CamfrogMultiID.exe'
$allowed = @(
    $exeName
    'CamfrogMultiID.pdb'
    'CamfrogMultiID.Core.pdb'
    'CamfrogMultiID.Infrastructure.pdb'
)

if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish directory not found: $PublishDir"
}

$resolved = (Resolve-Path -LiteralPath $PublishDir).Path
$files = @(Get-ChildItem -LiteralPath $resolved -Recurse -File)
$relative = @($files | ForEach-Object {
    $_.FullName.Substring($resolved.Length).TrimStart('\', '/') -replace '\\', '/'
} | Sort-Object)

Write-Host "== Publish allow-list verification: $resolved ==" -ForegroundColor Cyan
$relative | ForEach-Object { Write-Host "  $_" }

$unexpected = @($relative | Where-Object { $allowed -notcontains $_ })
if ($unexpected.Count -gt 0) {
    throw "Unexpected file(s) in the publish output: $($unexpected -join ', ')"
}

$missing = @($allowed | Where-Object { $relative -notcontains $_ })
if ($missing.Count -gt 0) {
    throw "Missing expected file(s) in the publish output: $($missing -join ', ')"
}

$exe = Join-Path $resolved $exeName
$exeFile = Get-Item -LiteralPath $exe
# A truncated or stub single-file bundle is a broken download, not a build.
if ($exeFile.Length -lt 20MB) {
    throw "Published executable is implausibly small ($($exeFile.Length) bytes): $exe"
}

$hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
Write-Host ''
Write-Host "Executable: $exe ($([math]::Round($exeFile.Length / 1MB, 1)) MB)" -ForegroundColor Green
Write-Host "SHA256: $hash"
Write-Host 'PUBLISH VERIFY OK' -ForegroundColor Green
