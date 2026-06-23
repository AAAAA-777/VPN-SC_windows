using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace VpnSc.Services;

public static class DeviceIdService
{
    private static string? _cachedDeviceId;

    /// <summary>
    /// Stable per-machine id for API: windows_ + MachineGuid (or volume serial fallback).
    /// </summary>
    public static string GetWindowsDeviceId()
    {
        if (_cachedDeviceId is { Length: > 0 })
            return _cachedDeviceId;

        var machineGuid = GetMachineGuid();
        if (machineGuid is { Length: > 0 })
        {
            _cachedDeviceId = "windows_" + machineGuid.ToLowerInvariant();
            return _cachedDeviceId;
        }

        var volumeSerial = GetSystemVolumeSerial();
        if (volumeSerial is { Length: > 0 })
        {
            _cachedDeviceId = "windows_" + volumeSerial;
            return _cachedDeviceId;
        }

        _cachedDeviceId = "windows_" + Guid.NewGuid().ToString("D").ToLowerInvariant();
        return _cachedDeviceId;
    }

    public static string GetMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            if (key?.GetValue("MachineGuid") is string value && value is { Length: > 0 })
                return value.Trim();
        }
        catch
        {
            /* ignore */
        }

        return "";
    }

    private static string GetSystemVolumeSerial()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(root))
                root = @"C:\";

            if (!GetVolumeInformation(
                    root,
                    null!,
                    0,
                    out var serial,
                    out _,
                    out _,
                    null!,
                    0))
                return "";

            return serial.ToString("x8");
        }
        catch
        {
            return "";
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformation(
        string lpRootPathName,
        StringBuilder? lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder? lpFileSystemNameBuffer,
        int nFileSystemNameSize);
}
