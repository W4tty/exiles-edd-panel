# Fails when any text file in the repo contains an en dash or an em dash (house rule: none anywhere).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$skip = '\\(\.git|bin|obj|dist|lib)\\'
$bad = @()
Get-ChildItem -Path $root -Recurse -File | Where-Object { $_.FullName -notmatch $skip -and $_.Extension -notin '.dll', '.pdb', '.png', '.zip' } | ForEach-Object {
    $text = [System.IO.File]::ReadAllText($_.FullName)
    if ($text.IndexOf([char]0x2013) -ge 0 -or $text.IndexOf([char]0x2014) -ge 0) { $bad += $_.FullName }
}
if ($bad.Count -gt 0) {
    Write-Output 'Dashes found in:'
    $bad | ForEach-Object { Write-Output "  $_" }
    exit 1
}
Write-Output 'no dashes'
