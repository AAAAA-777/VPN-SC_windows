using System.Net.Http;
using System.Text.Json;
using VpnSc.Models;

namespace VpnSc.Services;

public sealed class WireguardApiException : Exception
{
    public string ResponseBody { get; }

    public WireguardApiException(string message, string responseBody = "")
        : base(message)
    {
        ResponseBody = responseBody;
    }
}

public static class WireguardApiService
{
    private const string BaseUrl = "https://vpn-sc.com/api/wireguard/";
    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VPN-SC APP");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return c;
    }

    public static async Task<List<WireguardServer>> FetchServersAsync(string accessToken)
    {
        var url = $"{BaseUrl}?id={Uri.EscapeDataString(accessToken)}";
        var body = await Http.GetStringAsync(url);
        body = body.Trim();
        if (string.IsNullOrEmpty(body) || body == "APP")
            throw new WireguardApiException("Invalid API response", body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.TryGetProperty("success", out var success) &&
            success.ValueKind == JsonValueKind.False)
        {
            var err = root.TryGetProperty("error", out var e) ? e.GetString() : "Request failed";
            throw new WireguardApiException(err ?? "Request failed", body);
        }

        return ParseServersList(root);
    }

    public static async Task<string> FetchConnectConfigAsync(string accessToken, string serverId)
    {
        var url =
            $"{BaseUrl}?action=connect&id={Uri.EscapeDataString(accessToken)}&server_id={Uri.EscapeDataString(serverId)}";
        var body = await Http.GetStringAsync(url);
        body = body.Trim();
        if (string.IsNullOrEmpty(body) || body == "APP")
            throw new WireguardApiException("Invalid API response", body);

        var payload = ExtractConfigPayload(body);
        if (string.IsNullOrWhiteSpace(payload))
            throw new WireguardApiException("Config not found in response", body);
        return payload;
    }

    public static string? ExtractConfigPayload(string rawBody)
    {
        var trimmed = rawBody.Trim();
        if (trimmed.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        if (trimmed.StartsWith("[Interface]", StringComparison.Ordinal))
            return trimmed;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "Connect failed";
                throw new WireguardApiException(err ?? "Connect failed", trimmed);
            }

            foreach (var key in new[]
                     {
                         "config", "vpn", "vpn_uri", "vpn_url", "data", "conf", "configuration"
                     })
            {
                if (root.TryGetProperty(key, out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }

            if (root.TryGetProperty("data", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "config", "vpn", "vpn_uri" })
                {
                    if (nested.TryGetProperty(key, out var value) &&
                        value.ValueKind == JsonValueKind.String)
                    {
                        var text = value.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }
            }
        }
        catch (WireguardApiException)
        {
            throw;
        }
        catch
        {
            /* not json */
        }

        return null;
    }

    private static List<WireguardServer> ParseServersList(JsonElement decoded)
    {
        JsonElement? list = null;
        if (decoded.TryGetProperty("servers", out var servers))
            list = servers;
        else if (decoded.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
                list = data;
            else if (data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("servers", out var nestedServers))
                    list = nestedServers;
                else if (data.TryGetProperty("list", out var nestedList))
                    list = nestedList;
            }
        }
        else if (decoded.TryGetProperty("list", out var listProp))
            list = listProp;

        if (list is not { ValueKind: JsonValueKind.Array })
            return new List<WireguardServer>();

        var result = new List<WireguardServer>();
        foreach (var item in list.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = ReadString(item, "server_id", "id", "serverId", "sid");
            if (string.IsNullOrEmpty(id))
                continue;
            var name = ReadString(item, "name", "title", "location", "country", "display_name", "server_name") ?? id;
            result.Add(new WireguardServer
            {
                Id = id,
                DisplayName = name,
                AutoConnect = ReadBool(item, "auto_connect", "autoConnect")
            });
        }
        return result;
    }

    private static string? ReadString(JsonElement map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!map.TryGetProperty(key, out var value))
                continue;
            var text = value.ToString().Trim();
            if (!string.IsNullOrEmpty(text))
                return text;
        }
        return null;
    }

    private static bool ReadBool(JsonElement map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!map.TryGetProperty(key, out var value))
                continue;
            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    return value.TryGetInt32(out var n) && n != 0;
                case JsonValueKind.String:
                    var text = value.GetString()?.Trim().ToLowerInvariant();
                    return text is "true" or "1";
            }
        }
        return false;
    }
}
