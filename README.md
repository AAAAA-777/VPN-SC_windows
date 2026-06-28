# VPN-SC (Windows) C#

WPF-клиент VPN Security Connect — .NET Framework 4.8, один exe для Windows 7 SP1 – 11.

## Сборка

Локально:

```powershell
cd s:\vpn_sc_v2
.\scripts\build.ps1
```

Продакшен `vpn.zip` (CDN / установщик):

```powershell
.\scripts\pack-vpn-zip.ps1
.\scripts\verify-vpn-zip.ps1
```

Подробности: [scripts/README.md](scripts/README.md)

Требуется: [.NET SDK](https://dotnet.microsoft.com/download) 8+ или Visual Studio 2022 с .NET desktop development.

## Стек

- C# / WPF / Wpf.Ui 4.x
- MVVM (CommunityToolkit.Mvvm)
- Навигация: ContentControl + enum AppPage (без NavigationView Frame)
- Xray: `Xray-win7-*` на Win7, `Xray-windows-*` на Win10+
- Шифрование: DPAPI (`%AppData%\VpnSecurityConnect\prefs.json`)

## Правило

Все изменения в `VPN-SC/`. Локальная сборка: `.\scripts\build.ps1`. Продакшен-архив: `.\scripts\pack-vpn-zip.ps1`
