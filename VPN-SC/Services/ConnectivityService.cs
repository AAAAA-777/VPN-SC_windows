using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class ConnectivityService
{
    private static readonly TimeSpan TotalProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CurlProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TcpProbeTimeout = TimeSpan.FromSeconds(5);

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

    public static Task<bool> HasInternetConnectionAsync() =>
        HasInternetConnectionAsync(CancellationToken.None);

    public static async Task<bool> HasInternetConnectionAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TotalProbeTimeout);
        var token = timeoutCts.Token;

        try
        {
            if (await ProbeWithCurlAsync(token))
                return true;

            if (await ProbeWithCurlResolvedAsync(token))
                return true;

            if (await ProbeTcpAsync(token))
                return true;

            foreach (var url in ProbeUrls)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.TryAddWithoutValidation("User-Agent", "VPN-SC APP");
                    using var resp = await Http.SendAsync(
                        req,
                        HttpCompletionOption.ResponseHeadersRead,
                        token);
                    if (IsReachableStatusCode((int)resp.StatusCode))
                        return true;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    /* try next */
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
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

    private static async Task<bool> ProbeWithCurlAsync(CancellationToken cancellationToken)
    {
        foreach (var url in ProbeUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ProbeUrlWithCurlAsync(url, cancellationToken))
                return true;
        }
        return false;
    }

    private static async Task<bool> ProbeWithCurlResolvedAsync(CancellationToken cancellationToken)
    {
        foreach (var (host, ip) in ResolvedProbeHosts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"https://{host}/generate_204";
            if (host == "www.microsoft.com")
                url = $"https://{host}/";
            if (await ProbeUrlWithCurlAsync(url, $"--resolve {host}:443:{ip} ", cancellationToken))
                return true;
        }
        return false;
    }

    private static async Task<bool> ProbeTcpAsync(CancellationToken cancellationToken)
    {
        foreach (var (host, port) in TcpProbeTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(TcpProbeTimeout, cancellationToken);
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    if (timeoutTask.IsCanceled)
                        throw new OperationCanceledException(cancellationToken);
                    continue;
                }
                if (!tcp.Connected)
                    continue;
                await connectTask;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                /* try next */
            }
        }
        return false;
    }

    private static Task<bool> ProbeUrlWithCurlAsync(
        string url,
        CancellationToken cancellationToken) =>
        ProbeUrlWithCurlAsync(url, extraArgs: "", cancellationToken);

    private static async Task<bool> ProbeUrlWithCurlAsync(
        string url,
        string extraArgs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(CurlProbeTimeout);
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
                if (cancellationToken.IsCancellationRequested)
                    throw;
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
