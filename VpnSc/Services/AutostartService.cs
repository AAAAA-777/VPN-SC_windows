using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace VpnSc.Services;

public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VpnSecurityConnect";

    public static bool IsEnabled()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        if (k?.GetValue(ValueName) is not string s || string.IsNullOrWhiteSpace(s))
            return false;
        var path = s.Trim().Trim('"');
        return File.Exists(path);
    }

    public static void SetEnabled(bool enabled)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (k == null)
            return;
        if (!enabled)
        {
            k.DeleteValue(ValueName, false);
            return;
        }

        var exe = GetExecutablePath();
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            return;

        k.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
    }

    private static string GetExecutablePath()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var path = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path))
                return path;
        }
        catch
        {
            /* ignore */
        }

        return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }
}

