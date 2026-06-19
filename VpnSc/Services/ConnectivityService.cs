using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace VpnSc.Services;

public static class ConnectivityService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static async Task<bool> HasInternetConnectionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var host = await Task.Run(() => Dns.GetHostAddresses("google.com"), cts.Token);
            if (host.Length == 0)
                return false;
        }
        catch
        {
            return false;
        }

        foreach (var url in new[]
                 {
                     "https://google.com",
                     "https://cloudflare.com",
                     "https://microsoft.com"
                 })
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", "VPN-SC APP");
                using var resp = await Http.SendAsync(req);
                if (resp.IsSuccessStatusCode)
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
}

