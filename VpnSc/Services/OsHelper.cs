using System;
using System.Runtime.InteropServices;

namespace VpnSc.Services;

public static class OsHelper
{
    public static bool IsWindows10OrGreater()
    {
        var v = GetOsVersion();
        return v.Major > 6 || (v.Major == 6 && v.Minor >= 2);
    }

    public static bool IsWindows7()
    {
        var v = GetOsVersion();
        return v.Major == 6 && v.Minor == 1;
    }

    public static Version GetOsVersion()
    {
        var ver = new OsVersionInfo { OSVersionInfoSize = Marshal.SizeOf(typeof(OsVersionInfo)) };
        if (!RtlGetVersion(ref ver))
            return Environment.OSVersion.Version;
        return new Version(ver.MajorVersion, ver.MinorVersion, ver.BuildNumber);
    }

    [DllImport("ntdll.dll")]
    private static extern bool RtlGetVersion(ref OsVersionInfo versionInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OsVersionInfo
    {
        public int OSVersionInfoSize;
        public int MajorVersion;
        public int MinorVersion;
        public int BuildNumber;
        public int PlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string CSDVersion;
        public ushort ServicePackMajor;
        public ushort ServicePackMinor;
        public ushort SuiteMask;
        public byte ProductType;
        public byte Reserved;
    }
}
