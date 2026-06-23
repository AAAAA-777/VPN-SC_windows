using System.Diagnostics;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class VpnTunnelProbe
{
    private static readonly string[] SocksTestUrls =
    {
        "https://www.gstatic.com/generate_204",
        "https://cp.cloudflare.com/generate_204",
        "https://www.google.com/generate_204"
    };

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    public static async Task<bool> MeasureThroughSocksAsync()
    {
        foreach (var url in SocksTestUrls)
        {
            if (await ProbeUrlThroughSocksAsync(url))
                return true;
        }
        return false;
    }

    public static async Task<bool> TestInternetAsync() =>
        await ConnectivityService.HasInternetConnectionAsync();

    private static async Task<bool> ProbeUrlThroughSocksAsync(string url)
    {
        return await ProbeUrlWithCurlAsync(url, useSocks: true);
    }

    private static async Task<bool> ProbeUrlWithCurlAsync(string url, bool useSocks)
    {
        try
        {
            var curl = ResolveCurlExecutable();
            var socks = useSocks ? "--socks5-hostname 127.0.0.1:1080 " : "";
            var psi = new ProcessStartInfo
            {
                FileName = curl,
                Arguments =
                    $"{socks}--max-time {TestTimeout.TotalSeconds:0} -s -o NUL -w %{{http_code}} \"{url}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return false;
            using var cts = new CancellationTokenSource(TestTimeout);
            try
            {
                await ProcessCompat.WaitForExitAsync(process, cts.Token);
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
                return false;
            }

            var code = (await process.StandardOutput.ReadToEndAsync()).Trim();
            return int.TryParse(code, out var status) && status is 200 or 204 or 301 or 302 or 304;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveCurlExecutable()
    {
        const string systemCurl = @"C:\Windows\System32\curl.exe";
        return File.Exists(systemCurl) ? systemCurl : "curl";
    }
}
