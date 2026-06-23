using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VpnSc.Services;

public static class SystemProxyService
{
    private const string SubKey =
        @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    private const int InternetOptionSettingsChanged = 39;
    private const int InternetOptionRefresh = 37;

    private const uint WmSettingChange = 0x001A;
    private static readonly IntPtr HwndBroadcast = new(0xffff);
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint SettingsChangeTimeoutMs = 5000;

    public static void EnableSystemProxy(string host, int port, string bypass = "<local>")
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SubKey, writable: true);
            if (key == null)
                return;
            key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            key.SetValue("ProxyServer", $"{host}:{port}", RegistryValueKind.String);
            key.SetValue("ProxyOverride", bypass, RegistryValueKind.String);
            NotifyProxySettingsChanged();
        }
        catch
        {
            /* ignore */
        }
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
            NotifyProxySettingsChanged();
        }
        catch
        {
            /* ignore */
        }
    }

    /// <summary>
    /// Notifies WinINet and running applications that per-user proxy settings changed.
    /// Works on Windows 7, 10, and 11 (WinINet + WM_SETTINGCHANGE).
    /// </summary>
    private static void NotifyProxySettingsChanged()
    {
        try
        {
            InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
        }
        catch
        {
            /* ignore */
        }

        try
        {
            _ = SendMessageTimeout(
                HwndBroadcast,
                WmSettingChange,
                UIntPtr.Zero,
                "Internet Settings",
                SmtoAbortIfHung,
                SettingsChangeTimeoutMs,
                out _);
        }
        catch
        {
            /* ignore */
        }
    }

    [DllImport("wininet.dll", SetLastError = true, EntryPoint = "InternetSetOptionW", CharSet = CharSet.Unicode)]
    private static extern bool InternetSetOption(
        IntPtr hInternet,
        int dwOption,
        IntPtr lpBuffer,
        int dwBufferLength);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out UIntPtr lpdwResult);
}
