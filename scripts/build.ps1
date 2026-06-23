# Build VPN Security Connect v2 (requires .NET SDK 8+ or Visual Studio 2022)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$nativeExe = Join-Path $Root "native\x64\awg_tunnel_service.exe"
if (-not (Test-Path $nativeExe)) {
    Write-Host "AWG binaries missing; running fetch_awg_binaries.ps1 ..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "fetch_awg_binaries.ps1")
}

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    dotnet restore VPN-SC\VPN-SC.csproj
    dotnet build VPN-SC\VPN-SC.csproj -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $out = Join-Path $Root "VPN-SC\bin\Release\net48"
    Write-Host "Output: $out\vpn-sc.exe" -ForegroundColor Green
    if (Test-Path (Join-Path $out "awg_tunnel_service.exe")) {
        Write-Host "AWG helper copied to output." -ForegroundColor Green
    }
    exit 0
}

$msbuild = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($msbuild) {
    & $msbuild VpnSc.sln /p:Configuration=Release /restore
    exit $LASTEXITCODE
}

Write-Error "Install .NET SDK or Visual Studio 2022 to build."
