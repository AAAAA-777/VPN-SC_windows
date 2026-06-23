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
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VPN-Security-Connect/1.0.7");
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

    public static bool CheckRequiredFiles()
    {
        foreach (var name in RequiredFiles.Keys)
        {
            if (!File.Exists(Path.Combine(GetConnectDirectory(), name)))
                return false;
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
                continue;

            log?.Report($"Downloading {kv.Key}...");
            if (await DownloadFileAsync(kv.Value, dest, log))
            {
                downloaded++;
                continue;
            }

            if (kv.Key == "xray.exe" && await DownloadAndExtractXrayFromGitHubAsync(log))
            {
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

    private static async Task<bool> DownloadFileAsync(
        string url,
        string dest,
        IProgress<string>? log = null)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            if (bytes.Length == 0)
                return false;
            await FileCompat.WriteAllBytesAsync(dest, bytes);
            return File.Exists(dest) && new FileInfo(dest).Length > 0;
        }
        catch (Exception ex)
        {
            log?.Report($"{Path.GetFileName(dest)} failed: {ex.Message}");
            return false;
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
                return File.Exists(dest);
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
