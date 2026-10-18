<#
.SYNOPSIS
  Installs (or removes) the Exiles Monitor panel for EDDiscovery.

.DESCRIPTION
  Copies ExilesMonitorPanel.dll, which must sit next to this script, into the DLL folder of
  EDDiscovery's data folder and clears the "downloaded file" block on it. EDDiscovery loads
  the DLL on its next start and asks once whether to allow it.

  Data folder rules (the same ones EDDiscovery uses):
    portable install      <EDDiscovery.exe folder>\EDDiscovery\   (EDD started with -portable)
    normal install        %LOCALAPPDATA%\EDDiscovery\
  The DLL goes into the DLL subfolder of that data folder. Pass -DllFolder if EDD runs with -appfolder.

.PARAMETER EddFolder
  Folder holding EDDiscovery.exe, used to detect a portable install. Default D:\WattysRig\Apps\EDDiscovery.

.PARAMETER DllFolder
  Use this folder instead of working it out.

.PARAMETER Uninstall
  Remove the DLL instead of installing it.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\install.ps1
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -EddFolder "C:\Program Files\EDDiscovery"
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
#>
param(
    [string]$EddFolder = 'D:\WattysRig\Apps\EDDiscovery',
    [string]$DllFolder = '',
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $here 'ExilesMonitorPanel.dll'
if (-not $Uninstall -and -not (Test-Path -LiteralPath $src)) {
    throw "ExilesMonitorPanel.dll is not next to this script ($here)."
}

if ($DllFolder -eq '') {
    $portableDb = Join-Path $EddFolder 'EDDiscovery\EDDUser.sqlite'
    $localDb = Join-Path $env:LOCALAPPDATA 'EDDiscovery\EDDUser.sqlite'
    if (Test-Path -LiteralPath $portableDb) {
        $DllFolder = Join-Path $EddFolder 'EDDiscovery\DLL'
        Write-Output "Portable EDDiscovery data found under $EddFolder"
    }
    elseif (Test-Path -LiteralPath $localDb) {
        $DllFolder = Join-Path $env:LOCALAPPDATA 'EDDiscovery\DLL'
        Write-Output "EDDiscovery data found under $env:LOCALAPPDATA\EDDiscovery"
    }
    else {
        throw "No EDDiscovery data folder found (looked for $portableDb and $localDb). Start EDDiscovery once, or pass -DllFolder."
    }
}

$running = Get-Process -Name EDDiscovery -ErrorAction SilentlyContinue
if ($running) {
    Write-Warning 'EDDiscovery is running. Close it, then run this script again.'
    exit 2
}

$dst = Join-Path $DllFolder 'ExilesMonitorPanel.dll'

if ($Uninstall) {
    if (Test-Path -LiteralPath $dst) {
        Remove-Item -LiteralPath $dst -Force
        Write-Output "Removed $dst"
    }
    else {
        Write-Output "Nothing to remove at $dst"
    }
    Write-Output 'Start EDDiscovery; the Exiles Monitor tab can then be closed from its tab menu.'
    exit 0
}

New-Item -ItemType Directory -Force -Path $DllFolder | Out-Null
Copy-Item -LiteralPath $src -Destination $dst -Force
Unblock-File -LiteralPath $dst
$hash = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash
Write-Output "Installed $dst"
Write-Output "SHA256 $hash"
Write-Output ''
Write-Output 'Next steps:'
Write-Output '  1. Start EDDiscovery. It asks "The following application extension DLL have been found. Do you wish to allow it to be used?" Click Yes.'
Write-Output '  2. Click the + tab at the right end of the tab strip and pick "Exiles Monitor". It opens as a new tab (or as a pop-out window from the same list).'
Write-Output '  3. If "Exiles Monitor" is not in the list, close EDDiscovery and start it again once.'
Write-Output '  4. The EDDiscovery Log panel shows "DLLs loaded: ExilesMonitorPanel" when all is well.'
