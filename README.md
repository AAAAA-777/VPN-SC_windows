# VPN-SC (Windows) C#

WPF-клиент VPN Security Connect — .NET Framework 4.8, один exe для Windows 7 SP1 – 11.

## Сборка

```powershell
cd s:\vpn_sc_v2
.\scripts\build.ps1
```

Требуется: [.NET SDK](https://dotnet.microsoft.com/download) 8+ или Visual Studio 2022 с .NET desktop development.

## Стек

- C# / WPF / Wpf.Ui 4.x
- MVVM (CommunityToolkit.Mvvm)
- Навигация: ContentControl + enum AppPage (без NavigationView Frame)
- Xray: `Xray-win7-*` на Win7, `Xray-windows-*` на Win10+
- Шифрование: XOR+MD5 как во Flutter (`%AppData%\VpnSecurityConnect\prefs.json`)

## Правило

Все изменения только в `s:\vpn_sc_v2`. `S:\vpn_sc` и `D:\awg` — только референс.
