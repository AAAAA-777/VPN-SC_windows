using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class ConnectivityService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    private static readonly string[] ProbeUrls =
    {
        "https://www.gstatic.com/generate_204",
        "https://cp.cloudflare.com/generate_204",
        "https://connect.vpn-sc.com/",
        "https://vpn-sc.com/",
        "https://www.microsoft.com/",
    };

    private static readonly (string Host, string Ip)[] ResolvedProbeHosts =
    {
        ("www.gstatic.com", "142.250.185.14"),
        ("cp.cloudflare.com", "104.16.132.229"),
        ("www.microsoft.com", "20.70.246.20"),
    };

    private static readonly (string Host, int Port)[] TcpProbeTargets =
    {
        ("1.1.1.1", 443),
        ("8.8.8.8", 443),
        ("104.16.132.229", 443),
    };

    public static async Task<bool> HasInternetConnectionAsync()
    {
        if (await ProbeWithCurlAsync())
            return true;

        if (await ProbeWithCurlResolvedAsync())
            return true;

        if (await ProbeTcpAsync())
            return true;

        foreach (var url in ProbeUrls)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", "VPN-SC APP");
                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (IsReachableStatusCode((int)resp.StatusCode))
                    return true;
            }
            catch
            {
                /* try next */
            }
        }

        return false;
    }

    public static bool IsNetworkError(Exception ex)
    {
        var s = ex.ToString().ToLowerInvariant();
        return ex is HttpRequestException || ex is TaskCanceledException || ex is SocketException
               || s.Contains("socket")
               || s.Contains("handshake")
               || s.Contains("connection refused")
               || s.Contains("timeout")
               || s.Contains("no route");
    }

    private static async Task<bool> ProbeWithCurlAsync()
    {
        foreach (var url in ProbeUrls)
        {
            if (await ProbeUrlWithCurlAsync(url))
                return true;
        }
        return false;
    }

    private static async Task<bool> ProbeWithCurlResolvedAsync()
    {
        foreach (var (host, ip) in ResolvedProbeHosts)
        {
            var url = $"https://{host}/generate_204";
            if (host == "www.microsoft.com")
                url = $"https://{host}/";
            if (await ProbeUrlWithCurlAsync(url, $"--resolve {host}:443:{ip} "))
                return true;
        }
        return false;
    }

    private static async Task<bool> ProbeTcpAsync()
    {
        foreach (var (host, port) in TcpProbeTargets)
        {
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(5000));
                if (completed != connectTask || !tcp.Connected)
                    continue;
                await connectTask;
                return true;
            }
            catch
            {
                /* try next */
            }
        }
        return false;
    }

    private static Task<bool> ProbeUrlWithCurlAsync(string url) =>
        ProbeUrlWithCurlAsync(url, extraArgs: "");

    private static async Task<bool> ProbeUrlWithCurlAsync(string url, string extraArgs)
    {
        try
        {
            var curl = ResolveCurlExecutable();
            var psi = new ProcessStartInfo
            {
                FileName = curl,
                Arguments = $"-s -o NUL -w %{{http_code}} --max-time 8 {extraArgs}\"{url}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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

            var codeText = (await process.StandardOutput.ReadToEndAsync()).Trim();
            return int.TryParse(codeText, out var code) && IsReachableStatusCode(code);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReachableStatusCode(int code) =>
        code is 200 or 204 or 301 or 302 or 304;

    private static string ResolveCurlExecutable()
    {
        const string systemCurl = @"C:\Windows\System32\curl.exe";
        return File.Exists(systemCurl) ? systemCurl : "curl";
    }
}
