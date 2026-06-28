<#
.SYNOPSIS
    Продакшен-сборка vpn.zip для CDN и установщика.

.DESCRIPTION
    Официальный скрипт упаковки релиза VPN-SC. Запускать перед выкладкой на
    https://vpn-sc.com/install/vpn.zip

    Делает: Release build -> staging -> vpn.zip в корне репозитория.

    Включает: vpn-sc.exe, *.dll, vpn-sc.exe.config, awg_tunnel_service.exe,
    wintun.dll, Assets/Flags.

    НЕ включает (скачивает клиент при первом запуске):
    connect/xray.exe, geoip.dat, geosite.dat, config.json, .xray-source, *.pdb.

    Для агентов Cursor: при запросе «продакшен билд» / «vpn.zip» — этот скрипт,
    не копировать bin/Release вручную и не тащить connect/ из output.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\scripts\pack-vpn-zip.ps1
#>
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$nativeExe = Join-Path $Root "native\x64\awg_tunnel_service.exe"
if (-not (Test-Path $nativeExe)) {
    Write-Host "AWG binaries missing; running fetch_awg_binaries.ps1 ..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "fetch_awg_binaries.ps1")
}

$env:NUGET_PACKAGES = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
dotnet build (Join-Path $Root "VPN-SC\VPN-SC.csproj") -c Release -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = Join-Path $Root "VPN-SC\bin\Release\net48"
$dist = Join-Path $Root "dist"
$staging = Join-Path $dist "vpn-staging"
$zip = Join-Path $Root "vpn.zip"

$excludeConnectFiles = @(
    "xray.exe", "geoip.dat", "geosite.dat", "config.json", ".xray-source"
)

New-Item -ItemType Directory -Path $dist -Force | Out-Null
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

New-Item -ItemType Directory -Path $staging -Force | Out-Null

# Корневые runtime-файлы
Get-ChildItem $out -File | Where-Object {
    $_.Extension -in ".exe", ".dll", ".config" -and $_.Extension -ne ".pdb"
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $staging
}

# Assets (флаги)
$assetsSrc = Join-Path $out "Assets"
if (Test-Path $assetsSrc) {
    Copy-Item $assetsSrc (Join-Path $staging "Assets") -Recurse
}

# Явно убираем отладку, если попала
Get-ChildItem $staging -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# connect не включаем — клиент создаст и скачает xray/geoip/geosite сам
$junkInStaging = @()
foreach ($name in $excludeConnectFiles) {
    $p = Join-Path $staging $name
    if (Test-Path $p) { $junkInStaging += $p }
}
$connectDir = Join-Path $staging "connect"
if (Test-Path $connectDir) { $junkInStaging += $connectDir }
foreach ($p in $junkInStaging) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }

# Проверка обязательных файлов
$required = @(
    "vpn-sc.exe",
    "vpn-sc.exe.config",
    "awg_tunnel_service.exe",
    "wintun.dll",
    "Wpf.Ui.dll"
)
$missing = $required | Where-Object { -not (Test-Path (Join-Path $staging $_)) }
if ($missing.Count -gt 0) {
    Write-Error ("Missing required files: " + ($missing -join ", "))
}

Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip -CompressionLevel Optimal -Force
Remove-Item $staging -Recurse -Force

$info = Get-Item $zip
$version = (Select-String -Path (Join-Path $Root "VPN-SC\VPN-SC.csproj") -Pattern "<Version>([^<]+)</Version>").Matches[0].Groups[1].Value

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
$bad = $archive.Entries | Where-Object {
    $n = $_.FullName.ToLowerInvariant()
    $n -match '\.pdb$' -or
    $n -match '^connect/' -or
    $n -match 'xray\.exe' -or
    $n -match 'geoip\.dat' -or
    $n -match 'geosite\.dat' -or
    $n -match 'config\.json' -or
    $n -match '\.xray-source'
}
$archive.Dispose()

if ($bad) {
    Write-Error ("Junk detected in zip: " + (($bad | ForEach-Object { $_.FullName }) -join ", "))
}

Write-Host "OK: $zip" -ForegroundColor Green
Write-Host "Version: $version"
Write-Host ("Size: {0:N2} MB" -f ($info.Length / 1MB))
Write-Host ("Files: {0}" -f (([IO.Compression.ZipFile]::OpenRead($zip).Entries.Count)))
