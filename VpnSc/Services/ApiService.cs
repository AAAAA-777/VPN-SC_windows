using System.Net.Http;
using System.Text.Json;

namespace VpnSc.Services;

public static class ApiService
{
    private const string BaseUrl = "https://vpn-sc.com/api/v1";
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return c;
    }

    public static async Task<JsonElement?> RequestAsync(string relativeQuery)
    {
        try
        {
            using var resp = await Http.GetAsync($"{BaseUrl}{relativeQuery}");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<JsonElement>(
                    $$"""{"success":false,"error":"Ошибка сервера: {{(int)resp.StatusCode}}"}""");
            if (string.IsNullOrWhiteSpace(body))
                return JsonSerializer.Deserialize<JsonElement>(
                    """{"success":false,"error":"Пустой ответ сервера"}""");
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(body);
            }
            catch (JsonException)
            {
                return JsonSerializer.Deserialize<JsonElement>(
                    """{"success":false,"error":"Некорректный JSON в ответе"}""");
            }
        }
        catch
        {
            return JsonSerializer.Deserialize<JsonElement>(
                """{"success":false,"error":"Ошибка подключения к серверу"}""");
        }
    }

    public static bool IsSuccess(JsonElement? el) =>
        el is { } e && e.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;

    private static bool JsonAsBool(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Number:
                return el.TryGetInt32(out var n) && n != 0;
            case JsonValueKind.String:
                var s = el.GetString();
                return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            default: return false;
        }
    }

    private static int JsonAsInt(JsonElement el, int fallback = 0)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed))
            return parsed;
        return fallback;
    }

    public static async Task<JsonElement?> AuthAsync(string email) =>
        await RequestAsync($"?action=auth&mail={Uri.EscapeDataString(email)}");

    public static async Task<JsonElement?> VerifyCodeAsync(
        string email,
        string code,
        string? deviceId = null,
        string? deviceName = null)
    {
        var q = new List<string>
        {
            "action=verify",
            $"mail={Uri.EscapeDataString(email)}",
            $"code={Uri.EscapeDataString(code)}"
        };
        if (!string.IsNullOrEmpty(deviceId))
            q.Add($"device_id={Uri.EscapeDataString(deviceId)}");
        if (!string.IsNullOrEmpty(deviceName))
            q.Add($"device_name={Uri.EscapeDataString(deviceName)}");
        return await RequestAsync($"?{string.Join("&", q)}");
    }

    public static async Task<JsonElement?> GetUserInfoAsync(string token) =>
        await RequestAsync($"?action=user_info&token={Uri.EscapeDataString(token)}");

    public static async Task<JsonElement?> CheckSessionAsync(string token) =>
        await RequestAsync($"?action=check_session&token={Uri.EscapeDataString(token)}");

    public static async Task<JsonElement?> LogoutAsync(string token) =>
        await RequestAsync($"?action=logout&token={Uri.EscapeDataString(token)}");

    public static async Task<JsonElement?> GetSessionsListAsync(string token) =>
        await RequestAsync($"?action=sessions_list&token={Uri.EscapeDataString(token)}");

    public static async Task<JsonElement?> TerminateSessionAsync(string token, string sessionId) =>
        await RequestAsync(
            $"?action=terminate_session&token={Uri.EscapeDataString(token)}&session_id={Uri.EscapeDataString(sessionId)}");

    public static async Task<(bool ok, bool hasSubscription, int days)> CheckSubscriptionAsync(string token)
    {
        try
        {
            var info = await GetUserInfoAsync(token);
            if (!IsSuccess(info) || info!.Value.TryGetProperty("user", out var user) == false ||
                user.ValueKind != JsonValueKind.Object)
                return (false, false, 0);

            var has = user.TryGetProperty("has_subscription", out var hs) && JsonAsBool(hs);
            var days = user.TryGetProperty("subscription_days", out var sd) ? JsonAsInt(sd) : 0;
            return (true, has, days);
        }
        catch
        {
            return (false, false, 0);
        }
    }
}
