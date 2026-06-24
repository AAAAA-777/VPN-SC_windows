using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using VpnSc.Localization;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class AutoUpdateService
{
    private const string FeedUrl = "https://vpn-sc.com/app/updates.php";
    private const string UserAgent = "VPN-SC APP";
    private const int FileLockRetryCount = 5;
    private static readonly TimeSpan FileLockRetryDelay = TimeSpan.FromMilliseconds(400);

    private static int _updateInProgress;

    private static readonly HttpClient FeedHttp = CreateHttpClient(useSystemProxy: false, TimeSpan.FromSeconds(30));
    private static readonly HttpClient DownloadHttp = CreateHttpClient(useSystemProxy: false, TimeSpan.FromMinutes(10));
    private static readonly HttpClient DownloadHttpWithProxy = CreateHttpClient(useSystemProxy: true, TimeSpan.FromMinutes(10));

    public static string GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "1.0.10" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private static HttpClient CreateHttpClient(bool useSystemProxy, TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = useSystemProxy,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler) { Timeout = timeout };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        return client;
    }

    private static bool IsRemoteNewerThanLocal(string current, string remote)
    {
        try
        {
            var c = current.Split('.').Select(int.Parse).ToArray();
            var r = remote.Split('.').Select(int.Parse).ToArray();
            var n = Math.Max(c.Length, r.Length);
            Array.Resize(ref c, n);
            Array.Resize(ref r, n);
            for (var i = 0; i < n; i++)
            {
                if (r[i] > c[i]) return true;
                if (r[i] < c[i]) return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> IsVersionBlockedAsync()
    {
        try
        {
            var current = GetCurrentVersion();
            var uri = $"{FeedUrl}?version={Uri.EscapeDataString(current)}";
            var xml = await FeedHttp.GetStringAsync(uri);
            var m = Regex.Match(xml, @"<minVersion>([^<]+)</minVersion>");
            if (!m.Success)
                return false;
            var min = m.Groups[1].Value.Trim();
            return IsRemoteNewerThanLocal(current, min);
        }
        catch
        {
            return false;
        }
    }

    public static async Task<(bool hasUpdate, string? downloadUrl, string latestVersion)> CheckForUpdatesAsync()
    {
        try
        {
            var current = GetCurrentVersion();
            var uri = $"{FeedUrl}?version={Uri.EscapeDataString(current)}";
            var xml = await FeedHttp.GetStringAsync(uri);
            return ParseUpdateFeed(xml, current);
        }
        catch
        {
            return (false, null, GetCurrentVersion());
        }
    }

    private static (bool hasUpdate, string? downloadUrl, string latestVersion) ParseUpdateFeed(
        string xml, string current)
    {
        var verM = Regex.Match(xml, @"sparkle:version=""([^""]+)""");
        var urlM = Regex.Match(xml, @"<enclosure[^>]*\surl=""([^""]+)""", RegexOptions.IgnoreCase);
        if (!urlM.Success)
            urlM = Regex.Match(xml, @"url=""([^""]+)""");
        if (!verM.Success || !urlM.Success)
            return (false, null, current);

        var latest = verM.Groups[1].Value.Trim();
        var url = NormalizeDownloadUrl(urlM.Groups[1].Value.Trim());
        var has = IsRemoteNewerThanLocal(current, latest);
        return (has, url, latest);
    }

    private static string NormalizeDownloadUrl(string url)
    {
        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            url = url.Substring(0, url.Length - 4) + ".exe";
        return url;
    }

    public static async Task<(bool ok, string? error)> StartUpdateAsync(
        string? knownDownloadUrl = null,
        IProgress<(int received, int? total)>? progress = null)
    {
        if (Interlocked.Exchange(ref _updateInProgress, 1) != 0)
            return (false, I18n.T("update_already_running"));

        try
        {
            string? url = knownDownloadUrl;
            if (string.IsNullOrEmpty(url))
            {
                var (has, downloadUrl, _) = await CheckForUpdatesAsync();
                if (!has || string.IsNullOrEmpty(downloadUrl))
                    return (false, "no_update");
                url = downloadUrl;
            }

            var stamp = DateTime.UtcNow.Ticks;
            var partPath = Path.Combine(Path.GetTempPath(), $"vpn_update_{stamp}.part");
            var installerPath = Path.Combine(Path.GetTempPath(), $"vpn_update_{stamp}.exe");

            var (downloaded, downloadError) = await DownloadUpdateFileAsync(url, partPath, installerPath, progress);
            if (!downloaded)
                return (false, FormatDownloadError(url, downloadError));

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                });
                await Task.Delay(2000);
                Environment.Exit(0);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, I18n.T("update_launch_failed", ("path", installerPath)) + " " + ex.Message);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _updateInProgress, 0);
        }
    }

    private static string FormatDownloadError(string url, string? reason)
    {
        var detail = string.IsNullOrWhiteSpace(reason) ? "download_failed" : reason;
        return $"{url}\n{detail}";
    }

    private static async Task<(bool ok, string? error)> DownloadUpdateFileAsync(
        string url,
        string partPath,
        string installerPath,
        IProgress<(int received, int? total)>? progress)
    {
        await PrepareDownloadPathsAsync(partPath, installerPath);

        if (await TryDownloadWithCurlAsync(url, partPath, installerPath))
            return (true, null);

        var direct = await TryDownloadWithHttpAsync(DownloadHttp, url, partPath, installerPath, progress);
        if (direct.ok)
            return direct;

        await PrepareDownloadPathsAsync(partPath, installerPath);
        var viaProxy = await TryDownloadWithHttpAsync(DownloadHttpWithProxy, url, partPath, installerPath, progress);
        if (viaProxy.ok)
            return viaProxy;

        return (false, viaProxy.error ?? direct.error ?? "download_failed");
    }

    private static async Task PrepareDownloadPathsAsync(string partPath, string installerPath)
    {
        await TryDeleteFileAsync(partPath);
        await TryDeleteFileAsync(installerPath);
    }

    private static async Task<(bool ok, string? error)> TryDownloadWithHttpAsync(
        HttpClient client,
        string url,
        string partPath,
        string installerPath,
        IProgress<(int received, int? total)>? progress)
    {
        for (var attempt = 0; attempt < FileLockRetryCount; attempt++)
        {
            try
            {
                await TryDeleteFileAsync(partPath);
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                    return (false, $"HTTP {(int)resp.StatusCode}");

                var total = resp.Content.Headers.ContentLength is { } len ? (int?)len : null;
                using (var fs = new FileStream(partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var stream = await resp.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[81920];
                    var received = 0;
                    int read;
                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        received += read;
                        progress?.Report((received, total));
                    }
                }

                return await FinalizeDownloadAsync(partPath, installerPath);
            }
            catch (IOException) when (attempt < FileLockRetryCount - 1)
            {
                await TryDeleteFileAsync(partPath);
                await Task.Delay(FileLockRetryDelay);
            }
            catch (Exception ex)
            {
                await TryDeleteFileAsync(partPath);
                return (false, ex.Message);
            }
        }

        await TryDeleteFileAsync(partPath);
        return (false, I18n.T("update_file_locked"));
    }

    private static async Task<bool> TryDownloadWithCurlAsync(string url, string partPath, string installerPath)
    {
        try
        {
            const string systemCurl = @"C:\Windows\System32\curl.exe";
            if (!File.Exists(systemCurl))
                return false;

            await TryDeleteFileAsync(partPath);
            var psi = new ProcessStartInfo
            {
                FileName = systemCurl,
                Arguments = $"-f -L --max-time 600 -A \"{UserAgent}\" -o \"{partPath}\" \"{url}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return false;

            var stderrTask = process.StandardError.ReadToEndAsync();
            using var waitCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(620000));
            try
            {
                await ProcessCompat.WaitForExitAsync(process, waitCts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        ProcessCompat.Kill(process);
                }
                catch
                {
                    /* ignore */
                }
                _ = await stderrTask;
                await TryDeleteFileAsync(partPath);
                return false;
            }

            _ = await stderrTask;
            if (process.ExitCode != 0)
            {
                await TryDeleteFileAsync(partPath);
                return false;
            }

            var finalized = await FinalizeDownloadAsync(partPath, installerPath);
            return finalized.ok;
        }
        catch
        {
            await TryDeleteFileAsync(partPath);
            return false;
        }
    }

    private static async Task<(bool ok, string? error)> FinalizeDownloadAsync(string partPath, string installerPath)
    {
        if (!IsValidInstallerFile(partPath, out var validationError))
        {
            await TryDeleteFileAsync(partPath);
            return (false, validationError);
        }

        for (var attempt = 0; attempt < FileLockRetryCount; attempt++)
        {
            try
            {
                await TryDeleteFileAsync(installerPath);
                File.Move(partPath, installerPath);
                return (true, null);
            }
            catch (IOException) when (attempt < FileLockRetryCount - 1)
            {
                await Task.Delay(FileLockRetryDelay);
            }
            catch (Exception ex)
            {
                await TryDeleteFileAsync(partPath);
                return (false, ex.Message);
            }
        }

        await TryDeleteFileAsync(partPath);
        return (false, I18n.T("update_file_locked"));
    }

    private static bool IsValidInstallerFile(string path, out string? error)
    {
        error = null;
        try
        {
            if (!File.Exists(path))
            {
                error = "file_missing";
                return false;
            }

            var info = new FileInfo(path);
            if (info.Length < 64 * 1024)
            {
                error = $"file_too_small ({info.Length} bytes)";
                return false;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var header = new byte[2];
            if (fs.Read(header, 0, 2) != 2 || header[0] != (byte)'M' || header[1] != (byte)'Z')
            {
                error = "not_exe";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static async Task TryDeleteFileAsync(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(200);
            }
            catch
            {
                return;
            }
        }
    }
}
