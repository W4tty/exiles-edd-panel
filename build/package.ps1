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

# The Windows setup (Inno Setup 6). Built when ISCC is on the PC (GitHub Actions installs it); the zip is always made.
$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    foreach ($p in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                     "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
        if (Test-Path -LiteralPath $p) { $iscc = $p; break }
    }
}
if ($iscc) {
    & $iscc /Q ("/DAppVersion=" + $Version.TrimStart('v')) ("/O" + $dist) (Join-Path $root 'setup\ExilesMonitorPanel.iss')
    if ($LASTEXITCODE -ne 0) { throw 'setup build failed' }
    Get-ChildItem $dist -Filter 'ExilesMonitorPanel-Setup-*.exe' | ForEach-Object { Write-Output ("setup  " + $_.FullName) }
}
else {
    Write-Warning 'Inno Setup (ISCC.exe) not found: zip only, no setup .exe'
}

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$dllhash = (Get-FileHash (Join-Path $bin 'ExilesMonitorPanel.dll') -Algorithm SHA256).Hash
Write-Output "zip    $zip"
Write-Output "sha256 $hash"
Write-Output "dll    $dllhash"
