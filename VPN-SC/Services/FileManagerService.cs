using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class FileManagerService
{
    private const string InstallBaseUrl = "https://vpn-sc.com/install";
    private const string XrayReleaseTag = "v25.12.8";
    private const string GeoipFallbackUrl =
        "https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/geoip.dat";
    private const string GeositeFallbackUrl =
        "https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/geosite.dat";
    public const string VpnDir = "connect";

    public static readonly IReadOnlyDictionary<string, string> RequiredFiles =
        new Dictionary<string, string>
        {
            ["xray.exe"] = $"{InstallBaseUrl}/xray.exe",
            ["geoip.dat"] = $"{InstallBaseUrl}/geoip.dat",
            ["geosite.dat"] = $"{InstallBaseUrl}/geosite.dat"
        };

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "VPN-Security-Connect/" + AutoUpdateService.GetCurrentVersion());
        return c;
    }

    public static string GetConnectDirectory() =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VpnDir);

    public static string GetXrayPath() =>
        Path.Combine(GetConnectDirectory(), "xray.exe");

    public static string GetXrayZipAssetName()
    {
        var x64 = Environment.Is64BitOperatingSystem;
        if (OsHelper.IsWindows7())
            return x64 ? "Xray-win7-64.zip" : "Xray-win7-32.zip";
        return x64 ? "Xray-windows-64.zip" : "Xray-windows-32.zip";
    }

    public static string GetXrayZipDownloadUrl() =>
        $"https://github.com/XTLS/Xray-core/releases/download/{XrayReleaseTag}/{GetXrayZipAssetName()}";

    /// <summary>CDN xray.exe — только Win10+ x64; Win7 и 32-bit качают с GitHub.</summary>
    private static bool ShouldDownloadXrayFromGitHub() =>
        OsHelper.IsWindows7() || !Environment.Is64BitOperatingSystem;

    private static string XraySourceMarkerPath() =>
        Path.Combine(GetConnectDirectory(), ".xray-source");

    private static bool IsXrayFromGitHub()
    {
        try
        {
            var marker = XraySourceMarkerPath();
            if (!File.Exists(marker))
                return false;
            return string.Equals(File.ReadAllText(marker).Trim(), "github", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void WriteXraySourceMarker(string source)
    {
        try
        {
            Directory.CreateDirectory(GetConnectDirectory());
            File.WriteAllText(XraySourceMarkerPath(), source, System.Text.Encoding.UTF8);
        }
        catch
        {
            /* ignore */
        }
    }

    private static void TryDeleteXraySourceMarker() => TryDeleteFile(XraySourceMarkerPath());

    private static bool IsValidXrayForPlatform(string path)
    {
        if (!IsValidVpnFile("xray.exe", path))
            return false;
        if (ShouldDownloadXrayFromGitHub() && !IsXrayFromGitHub())
            return false;
        return true;
    }

    public static bool CheckRequiredFiles()
    {
        foreach (var name in RequiredFiles.Keys)
        {
            var path = Path.Combine(GetConnectDirectory(), name);
            var valid = string.Equals(name, "xray.exe", StringComparison.OrdinalIgnoreCase)
                ? IsValidXrayForPlatform(path)
                : IsValidVpnFile(name, path);
            if (!valid)
            {
                TryDeleteFile(path);
                if (string.Equals(name, "xray.exe", StringComparison.OrdinalIgnoreCase))
                    TryDeleteXraySourceMarker();
                return false;
            }
        }
        return true;
    }

    public static async Task<(bool ok, string message)> DownloadMissingFilesAsync(
        IProgress<string>? log = null)
    {
        Directory.CreateDirectory(GetConnectDirectory());
        var downloaded = 0;
        var failed = new List<string>();

        foreach (var kv in RequiredFiles)
        {
            var dest = Path.Combine(GetConnectDirectory(), kv.Key);
            if (File.Exists(dest))
            {
                var valid = string.Equals(kv.Key, "xray.exe", StringComparison.OrdinalIgnoreCase)
                    ? IsValidXrayForPlatform(dest)
                    : IsValidVpnFile(kv.Key, dest);
                if (valid)
                    continue;
                TryDeleteFile(dest);
                if (string.Equals(kv.Key, "xray.exe", StringComparison.OrdinalIgnoreCase))
                    TryDeleteXraySourceMarker();
            }

            log?.Report($"Downloading {kv.Key}...");

            if (string.Equals(kv.Key, "xray.exe", StringComparison.OrdinalIgnoreCase) &&
                ShouldDownloadXrayFromGitHub())
            {
                if (await DownloadAndExtractXrayFromGitHubAsync(log))
                {
                    WriteXraySourceMarker("github");
                    downloaded++;
                    continue;
                }

                failed.Add(kv.Key);
                continue;
            }

            if (await DownloadFileAsync(kv.Value, dest, kv.Key, log))
            {
                if (string.Equals(kv.Key, "xray.exe", StringComparison.OrdinalIgnoreCase))
                    WriteXraySourceMarker("cdn");
                downloaded++;
                continue;
            }

            if (await TryDownloadFallbackAsync(kv.Key, dest, log))
            {
                if (string.Equals(kv.Key, "xray.exe", StringComparison.OrdinalIgnoreCase))
                    WriteXraySourceMarker("github");
                downloaded++;
                continue;
            }

            failed.Add(kv.Key);
        }

        if (failed.Count == 0)
        {
            var msg = downloaded > 0
                ? $"Downloaded: {downloaded}"
                : "All files present";
            return (true, msg);
        }

        return (false, "Failed: " + string.Join(", ", failed));
    }

    private static async Task<bool> TryDownloadFallbackAsync(
        string fileName,
        string dest,
        IProgress<string>? log)
    {
        return fileName switch
        {
            "xray.exe" => await DownloadAndExtractXrayFromGitHubAsync(log),
            "geoip.dat" => await DownloadFileAsync(GeoipFallbackUrl, dest, fileName, log),
            "geosite.dat" => await DownloadFileAsync(GeositeFallbackUrl, dest, fileName, log),
            _ => false
        };
    }

    private static async Task<bool> DownloadFileAsync(
        string url,
        string dest,
        string expectedFileName,
        IProgress<string>? log = null)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            if (bytes.Length == 0)
                return false;
            await FileCompat.WriteAllBytesAsync(dest, bytes);
            if (!IsValidVpnFile(expectedFileName, dest))
            {
                log?.Report($"{expectedFileName}: invalid file from {url}");
                TryDeleteFile(dest);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            log?.Report($"{Path.GetFileName(dest)} failed: {ex.Message}");
            TryDeleteFile(dest);
            return false;
        }
    }

    private static bool IsValidVpnFile(string fileName, string path)
    {
        if (!File.Exists(path))
            return false;

        var info = new FileInfo(path);
        if (info.Length < 1024)
            return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var header = new byte[16];
            var read = fs.Read(header, 0, header.Length);
            if (read >= 2 && LooksLikeHtml(header, read))
                return false;

            if (string.Equals(fileName, "xray.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (info.Length < 512 * 1024)
                    return false;
                return header[0] == (byte)'M' && header[1] == (byte)'Z';
            }

            if (fileName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                return info.Length > 1024 * 1024;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeHtml(byte[] header, int read)
    {
        if (read < 2)
            return false;

        if (header[0] == (byte)'<' && header[1] == (byte)'!')
            return true;
        if (read >= 5 &&
            header[0] == (byte)'<' &&
            header[1] == (byte)'h' &&
            header[2] == (byte)'t' &&
            header[3] == (byte)'m' &&
            header[4] == (byte)'l')
            return true;

        return false;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }

    private static async Task<bool> DownloadAndExtractXrayFromGitHubAsync(IProgress<string>? log = null)
    {
        log?.Report($"Downloading {GetXrayZipAssetName()} from GitHub...");
        try
        {
            var zipBytes = await Http.GetByteArrayAsync(GetXrayZipDownloadUrl());
            var tempZip = Path.Combine(Path.GetTempPath(), $"xray_{Guid.NewGuid():N}.zip");
            var tempDir = Path.Combine(Path.GetTempPath(), $"xray_extract_{Guid.NewGuid():N}");
            try
            {
                await FileCompat.WriteAllBytesAsync(tempZip, zipBytes);
                ZipFile.ExtractToDirectory(tempZip, tempDir);
                var xraySrc = Path.Combine(tempDir, "xray.exe");
                if (!File.Exists(xraySrc))
                {
                    xraySrc = Directory.GetFiles(tempDir, "xray.exe", SearchOption.AllDirectories)
                        .FirstOrDefault() ?? "";
                    if (string.IsNullOrEmpty(xraySrc))
                        return false;
                }
                var dest = GetXrayPath();
                File.Copy(xraySrc, dest, true);
                if (!IsValidVpnFile("xray.exe", dest))
                {
                    TryDeleteFile(dest);
                    return false;
                }

                return true;
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }
        catch (Exception ex)
        {
            log?.Report($"Xray zip failed: {ex.Message}");
            return false;
        }
    }
}
