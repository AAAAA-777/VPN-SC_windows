using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VpnSc.Services;

public static class AnalyticsService
{
    private const string AnalyticsUrl = "https://vpn-sc.com/app/analytics.php";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task SendAnalyticsAsync()
    {
        try
        {
            var userData = await StorageService.GetUserDataAsync();
            var email = "-";
            var uuid = "-";
            if (userData != null)
            {
                if (userData.Value.TryGetProperty("mail", out var m))
                    email = m.GetString() ?? "-";
                if (userData.Value.TryGetProperty("uuid", out var u))
                    uuid = u.GetString() ?? "-";
            }

            var appVersion = AutoUpdateService.GetCurrentVersion();
            var payload = new
            {
                user = new { email, uuid },
                system = new
                {
                    uuid = GetMachineGuid(),
                    os = Environment.OSVersion.Platform.ToString(),
                    os_version = Environment.OSVersion.VersionString,
                    version = appVersion,
                    app_name = "VPN Security Connect",
                    ip_address = await GetExternalIpAsync()
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.TryAddWithoutValidation("User-Agent", "VPN-SC APP");
            using var resp = await Http.PostAsync(AnalyticsUrl, content);
            _ = await resp.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            /* ignore */
        }
    }

    private static string GetMachineGuid()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var v = k?.GetValue("MachineGuid") as string;
            return string.IsNullOrWhiteSpace(v) ? "-" : v.Trim();
        }
        catch
        {
            return "-";
        }
    }

    private static async Task<string> GetExternalIpAsync()
    {
        foreach (var url in new[]
                 {
                     "https://api.ipify.org?format=json",
                     "https://ipinfo.io/json",
                     "http://ip-api.com/json"
                 })
        {
            try
            {
                using var resp = await Http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                    continue;
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("ip", out var ip))
                    return ip.GetString() ?? "-";
                if (doc.RootElement.TryGetProperty("query", out var q))
                    return q.GetString() ?? "-";
            }
            catch
            {
                /* next */
            }
        }

        return "-";
    }
}
