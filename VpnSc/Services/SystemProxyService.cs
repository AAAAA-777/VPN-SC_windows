using Microsoft.Win32;

namespace VpnSc.Services;

public static class SystemProxyService
{
    private const string SubKey =
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public static void EnableSystemProxy(string host, int port, string bypass = "<local>")
    {
        using var key = Registry.CurrentUser.OpenSubKey(SubKey, writable: true);
        if (key == null)
            return;
        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", $"{host}:{port}", RegistryValueKind.String);
        key.SetValue("ProxyOverride", bypass, RegistryValueKind.String);
    }

    public static void DisableSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SubKey, writable: true);
            if (key == null)
                return;
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            key.SetValue("ProxyServer", "", RegistryValueKind.String);
        }
        catch
        {
            /* ignore */
        }
    }
}
