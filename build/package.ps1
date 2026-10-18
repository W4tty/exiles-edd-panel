<#
.SYNOPSIS
  Builds the panel and zips it with the install script and the install notes into dist\.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File build\package.ps1 -Version v1.0.0
#>
param(
    [string]$Version = 'dev',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $root 'src\ExilesMonitorPanel\ExilesMonitorPanel.csproj'
$bin = Join-Path $root 'src\ExilesMonitorPanel\bin\Release'
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'stage'

if (-not $NoBuild) {
    dotnet build $proj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item (Join-Path $bin 'ExilesMonitorPanel.dll') $stage
Copy-Item (Join-Path $root 'install.ps1') $stage
Copy-Item (Join-Path $root 'INSTALL.txt') $stage
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $stage 'LICENSE.txt')

$zip = Join-Path $dist ("ExilesMonitorPanel-" + $Version + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item $stage -Recurse -Force

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$dllhash = (Get-FileHash (Join-Path $bin 'ExilesMonitorPanel.dll') -Algorithm SHA256).Hash
Write-Output "zip    $zip"
Write-Output "sha256 $hash"
Write-Output "dll    $dllhash"
