using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VpnSc.Services;

public static class AutoUpdateService
{
    private const string FeedUrl = "https://vpn-sc.com/app/updates.php";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static void Initialize()
    {
        /* placeholder вЂ” РєР°Рє РІРѕ Flutter */
    }

    public static string GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "1.0.3" : $"{v.Major}.{v.Minor}.{v.Build}";
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
            var xml = await Http.GetStringAsync(uri);
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
            var xml = await Http.GetStringAsync(uri);
            var verM = Regex.Match(xml, @"sparkle:version=""([^""]+)""");
            var urlM = Regex.Match(xml, @"url=""([^""]+)""");
            if (!verM.Success || !urlM.Success)
                return (false, null, current);
            var latest = verM.Groups[1].Value;
            var url = urlM.Groups[1].Value;
            if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(0, url.Length - 4) + ".exe";
            var has = IsRemoteNewerThanLocal(current, latest);
            return (has, url, latest);
        }
        catch
        {
            return (false, null, GetCurrentVersion());
        }
    }

    public static async Task<bool> StartUpdateAsync(IProgress<(int received, int? total)>? progress = null)
    {
        var (has, url, _) = await CheckForUpdatesAsync();
        if (!has || string.IsNullOrEmpty(url))
            return false;

        try
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength is { } len ? (int?)len : null;
            var tmp = Path.Combine(Path.GetTempPath(), $"vpn_update_{DateTime.UtcNow.Ticks}.exe");
            using var fs = File.Create(tmp);
            using var stream = await resp.Content.ReadAsStreamAsync();
            var buffer = new byte[81920];
            var received = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read);
                received += read;
                progress?.Report((received, total));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = tmp,
                UseShellExecute = true
            });
            await Task.Delay(2000);
            Environment.Exit(0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}


