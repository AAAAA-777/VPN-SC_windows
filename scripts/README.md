# Скрипты сборки VPN-SC

## Для Cursor / агентов

**Продакшен-дистрибутив `vpn.zip`** — только через `pack-vpn-zip.ps1`.  
Не архивировать `VPN-SC\bin\Release\net48` целиком: в output может быть `connect\` с xray/geoip из локальных тестов.

| Скрипт | Назначение |
|--------|------------|
| `build.ps1` | Локальная Release-сборка → `VPN-SC\bin\Release\net48\` |
| `pack-vpn-zip.ps1` | **Продакшен:** Release + чистый `vpn.zip` в корне репо |
| `verify-vpn-zip.ps1` | Проверка `vpn.zip` (нет мусора, MZ у exe) |
| `fetch_awg_binaries.ps1` | Скачать `awg_tunnel_service.exe` / `wintun.dll` в `native\x64\` |

## Продакшен

```powershell
cd s:\vpn_sc_v2
powershell -ExecutionPolicy Bypass -File .\scripts\pack-vpn-zip.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-vpn-zip.ps1
```

Результат: `vpn.zip` (~11 MB) → залить на CDN `https://vpn-sc.com/install/vpn.zip`.

### Что внутри vpn.zip

- `vpn-sc.exe`, `vpn-sc.exe.config`, все runtime DLL
- `awg_tunnel_service.exe`, `wintun.dll`
- `Assets\Flags\*.svg`

### Что не должно попасть в vpn.zip

- `*.pdb`
- `connect\` и всё внутри: `xray.exe`, `geoip.dat`, `geosite.dat`, `config.json`, `.xray-source`

Эти файлы клиент подтягивает сам (`FileManagerService` — CDN / GitHub).
