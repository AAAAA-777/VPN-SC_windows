# Fetch AWG native binaries from D:\awg (read-only source) into vpn_sc_v2\native
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Dest = Join-Path $Root "native\x64"
$Src = "D:\awg\packages\awg_tunnel_windows\windows\bin\x64"
$BuildScript = "D:\awg\scripts\build_tunnel_dll.ps1"

New-Item -ItemType Directory -Force -Path $Dest | Out-Null

if (Test-Path $BuildScript) {
    Write-Host "Building AWG tunnel via D:\awg\scripts\build_tunnel_dll.ps1 ..."
    & $BuildScript
}

if (-not (Test-Path $Src)) {
    Write-Error "Source not found: $Src. Build AWG tunnel in D:\awg first."
}

Copy-Item -Force (Join-Path $Src "awg_tunnel_service.exe") $Dest
Copy-Item -Force (Join-Path $Src "wintun.dll") $Dest -ErrorAction SilentlyContinue
Write-Host "Copied AWG binaries to $Dest" -ForegroundColor Green
