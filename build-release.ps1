$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location -LiteralPath $PSScriptRoot

$solution = Join-Path $PSScriptRoot 'CamfrogMultiID.sln'
$project = Join-Path $PSScriptRoot 'src\CamfrogMultiID.App\CamfrogMultiID.App.csproj'
$publishDir = Join-Path $PSScriptRoot 'src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish'

if ($env:OS -ne 'Windows_NT') { throw 'Run this WPF build on Windows.' }

Write-Host '== Camfrog Multi-ID Production Build V12 - UI CULTURE + STARTUP + BUILD-GATE FIX ==' -ForegroundColor Cyan
dotnet --version
dotnet sln $solution list

Write-Host 'Restoring application project graph...' -ForegroundColor Yellow
dotnet restore $project --force-evaluate -r win-x64

$assets = Join-Path $PSScriptRoot 'src\CamfrogMultiID.App\obj\project.assets.json'
if (-not (Test-Path -LiteralPath $assets)) {
    throw "Restore did not create project.assets.json: $assets"
}

Write-Host 'Building application...' -ForegroundColor Yellow
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE. Publishing was skipped."
}

Write-Host 'Publishing self-contained win-x64...' -ForegroundColor Yellow
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $publishDir 'CamfrogMultiID.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Published executable was not created: $exe"
}

Write-Host ''
Write-Host 'BUILD SUCCESS' -ForegroundColor Green
Write-Host "Executable: $exe"
Write-Host "Publish directory: $publishDir"
