<#
.SYNOPSIS
    Проверка vpn.zip после pack-vpn-zip.ps1.

.DESCRIPTION
    Ищет мусор (pdb, connect/, xray, geoip, geosite) и проверяет MZ у vpn-sc.exe.
    Парный скрипт к pack-vpn-zip.ps1 — не для ручной сборки архива.
#>
param([string]$Zip = (Join-Path (Split-Path -Parent $PSScriptRoot) "vpn.zip"))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [IO.Compression.ZipFile]::OpenRead($Zip)
Write-Host "Zip: $Zip"
Write-Host "Entries: $($z.Entries.Count)"
$bad = $z.Entries | Where-Object {
    $n = $_.FullName.ToLowerInvariant()
    $n -match '\.pdb$' -or $n -match '^connect/' -or $n -match 'xray' -or
    $n -match 'geoip' -or $n -match 'geosite' -or $n -match 'config\.json'
}
if ($bad) { Write-Host "JUNK:" -ForegroundColor Red; $bad | ForEach-Object { $_.FullName } }
else { Write-Host "No junk patterns." -ForegroundColor Green }
$roots = $z.Entries | Where-Object { $_.FullName -notmatch '/' } | ForEach-Object { $_.FullName } | Sort-Object
Write-Host "Root files ($($roots.Count)):"
$roots | ForEach-Object { Write-Host "  $_" }
$e = $z.GetEntry("vpn-sc.exe")
$buf = New-Object byte[] 2
$s = $e.Open(); $s.Read($buf, 0, 2) | Out-Null; $s.Close(); $z.Dispose()
if ($buf[0] -eq 0x4D -and $buf[1] -eq 0x5A) { Write-Host "vpn-sc.exe: MZ OK" -ForegroundColor Green }
else { Write-Host "vpn-sc.exe: INVALID" -ForegroundColor Red; exit 1 }
