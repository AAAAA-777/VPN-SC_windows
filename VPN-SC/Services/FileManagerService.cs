using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class FileManagerService
{
    private const string InstallBaseUrl = "https://vpn-sc.com/install";
    private const string XrayReleaseTag = "v25.12.8";
    public const string VpnDir = "connect";

    public static readonly IReadOnlyDictionary<string, string> GeoFiles =
        new Dictionary<string, string>
        {
            ["geoip.dat"] = $"{InstallBaseUrl}/geoip.dat",
            ["geosite.dat"] = $"{InstallBaseUrl}/geosite.dat"
        };

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VPN-Security-Connect/1.0.4");
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

    public static string GetXrayDownloadUrl() =>
        $"https://github.com/XTLS/Xray-core/releases/download/{XrayReleaseTag}/{GetXrayZipAssetName()}";

    public static bool CheckRequiredFiles()
    {
        if (!File.Exists(GetXrayPath()))
            return false;
        foreach (var name in GeoFiles.Keys)
        {
            if (!File.Exists(Path.Combine(GetConnectDirectory(), name)))
                return false;
        }
        return true;
    }

    public static async Task<(bool anyDownloaded, string message)> DownloadMissingFilesAsync(
        IProgress<string>? log = null)
    {
        Directory.CreateDirectory(GetConnectDirectory());
        var downloaded = 0;

        if (!File.Exists(GetXrayPath()))
        {
            log?.Report($"Downloading {GetXrayZipAssetName()}...");
            try
            {
                if (await DownloadAndExtractXrayAsync())
                    downloaded++;
            }
            catch (Exception ex)
            {
                log?.Report($"Xray failed: {ex.Message}");
            }
        }

        foreach (var kv in GeoFiles)
        {
            var dest = Path.Combine(GetConnectDirectory(), kv.Key);
            if (File.Exists(dest))
                continue;
            log?.Report($"Downloading {kv.Key}...");
            try
            {
                var bytes = await Http.GetByteArrayAsync(kv.Value);
                await FileCompat.WriteAllBytesAsync(dest, bytes);
                if (bytes.Length > 0)
                    downloaded++;
            }
            catch
            {
                log?.Report($"Failed: {kv.Key}");
            }
        }

        var msg = downloaded > 0
            ? $"Downloaded: {downloaded}"
            : "All files present or download failed";
        return (downloaded > 0, msg);
    }

    private static async Task<bool> DownloadAndExtractXrayAsync()
    {
        var url = GetXrayDownloadUrl();
        var zipBytes = await Http.GetByteArrayAsync(url);
        var tempZip = Path.Combine(Path.GetTempPath(), $"xray_{Guid.NewGuid():N}.zip");
        var tempDir = Path.Combine(Path.GetTempPath(), $"xray_extract_{Guid.NewGuid():N}");
        try
        {
            await FileCompat.WriteAllBytesAsync(tempZip, zipBytes);
            ZipFile.ExtractToDirectory(tempZip, tempDir);
            var xraySrc = Path.Combine(tempDir, "xray.exe");
            if (!File.Exists(xraySrc))
            {
                xraySrc = Directory.GetFiles(tempDir, "xray.exe", SearchOption.AllDirectories).FirstOrDefault() ?? "";
                if (string.IsNullOrEmpty(xraySrc))
                    return false;
            }
            var dest = GetXrayPath();
            File.Copy(xraySrc, dest, true);
            return File.Exists(dest);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }
}

