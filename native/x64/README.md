# Native binaries

Place **`awg_tunnel_service.exe`** and **`wintun.dll`** in this folder.

Build from the repo root:

```powershell
.\scripts\build_tunnel_dll.ps1
```

Flutter copies both files next to your app `.exe` when you build for Windows.
