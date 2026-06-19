using VpnSc.Helpers;
using System.IO;
using System.Text.Json;

namespace VpnSc.Services;

public static class StorageService
{
    private static string StoragePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VpnSecurityConnect",
            "prefs.json");

    public sealed class PrefsDto
    {
        public string? access_token { get; set; }
        public string? user_data { get; set; }
        public bool? is_logged_in { get; set; }
        public string? awg_vpn_uri { get; set; }
        public string? vpn_protocol { get; set; }
        public string? legacy_protocol { get; set; }
        public bool? fragmentation_enabled { get; set; }
        public string? fragmentation_packets { get; set; }
        public string? fragmentation_length { get; set; }
        public string? fragmentation_interval { get; set; }
        public string? fragmentation_max_split { get; set; }
        public bool? fragmentation_auto { get; set; }
        public int? fragmentation_last_profile { get; set; }
        public bool? fingerprint_auto { get; set; }
        public string? fingerprint_override { get; set; }
        public double? window_left { get; set; }
        public double? window_top { get; set; }
    }

    public static async Task<PrefsDto> LoadPrefsAsync() => await LoadAsync();

    public static async Task SavePrefsAsync(PrefsDto dto) => await SaveAsync(dto);

    private static async Task<PrefsDto> LoadAsync()
    {
        try
        {
            if (!File.Exists(StoragePath))
                return new PrefsDto();
            using var fs = File.OpenRead(StoragePath);
            var dto = await JsonSerializer.DeserializeAsync<PrefsDto>(fs);
            return dto ?? new PrefsDto();
        }
        catch
        {
            return new PrefsDto();
        }
    }

    private static async Task SaveAsync(PrefsDto dto)
    {
        var dir = Path.GetDirectoryName(StoragePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        using var fs = File.Create(StoragePath);
        await JsonSerializer.SerializeAsync(fs, dto,
            new JsonSerializerOptions { WriteIndented = true });
    }

    public static async Task MigrateUnencryptedDataAsync()
    {
        var dto = await LoadAsync();
        if (dto.access_token is { } rawToken && !EncryptionService.IsEncrypted(rawToken))
            await SaveAccessTokenAsync(rawToken);
        if (dto.user_data is { } rawUser && !EncryptionService.IsEncrypted(rawUser))
        {
            try
            {
                var user = JsonSerializer.Deserialize<JsonElement>(rawUser);
                await SaveUserDataAsync(user);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    public static async Task SaveAccessTokenAsync(string token)
    {
        var dto = await LoadAsync();
        dto.access_token = EncryptionService.Encrypt(token);
        await SaveAsync(dto);
    }

    public static async Task<string?> GetAccessTokenAsync()
    {
        var dto = await LoadAsync();
        if (string.IsNullOrEmpty(dto.access_token))
            return null;
        try
        {
            return EncryptionService.Decrypt(dto.access_token);
        }
        catch
        {
            return null;
        }
    }

    public static async Task SaveUserDataAsync(JsonElement userData)
    {
        var dto = await LoadAsync();
        var json = JsonSerializer.Serialize(userData);
        dto.user_data = EncryptionService.Encrypt(json);
        await SaveAsync(dto);
    }

    public static async Task<JsonElement?> GetUserDataAsync()
    {
        var dto = await LoadAsync();
        if (string.IsNullOrEmpty(dto.user_data))
            return null;
        try
        {
            var decrypted = EncryptionService.Decrypt(dto.user_data);
            return JsonSerializer.Deserialize<JsonElement>(decrypted);
        }
        catch
        {
            return null;
        }
    }

    public static async Task SetLoggedInAsync(bool value)
    {
        var dto = await LoadAsync();
        dto.is_logged_in = value;
        await SaveAsync(dto);
    }

    public static async Task<bool> IsLoggedInAsync()
    {
        var dto = await LoadAsync();
        return dto.is_logged_in == true;
    }

    public static async Task ClearAllAsync()
    {
        var dto = await LoadAsync();
        var windowLeft = dto.window_left;
        var windowTop = dto.window_top;
        dto = new PrefsDto
        {
            window_left = windowLeft,
            window_top = windowTop
        };
        await SaveAsync(dto);
    }

    public static async Task ClearAllWithLogoutAsync()
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
                await ApiService.LogoutAsync(token);
        }
        catch
        {
            /* continue */
        }

        await ClearAllAsync();
    }

    public static async Task<string?> GetUserUuidAsync()
    {
        var user = await GetUserDataAsync();
        if (user == null)
            return null;
        if (user.Value.TryGetProperty("uuid", out var p))
            return p.GetString();
        return null;
    }
    public static async Task SaveAwgVpnUriAsync(string vpnUri)
    {
        var dto = await LoadAsync();
        dto.awg_vpn_uri = EncryptionService.Encrypt(vpnUri);
        await SaveAsync(dto);
    }

    public static async Task<string?> GetAwgVpnUriAsync()
    {
        var dto = await LoadAsync();
        if (string.IsNullOrEmpty(dto.awg_vpn_uri)) return null;
        try { return EncryptionService.Decrypt(dto.awg_vpn_uri); }
        catch { return null; }
    }

    public static async Task SaveVpnProtocolAsync(VpnProtocol protocol)
    {
        var dto = await LoadAsync();
        dto.vpn_protocol = protocol.StorageValue();
        dto.legacy_protocol = VpnProtocolExtensions.LegacyLabel(protocol);
        await SaveAsync(dto);
    }

    public static async Task<VpnProtocol> GetVpnProtocolAsync()
    {
        var dto = await LoadAsync();
        var stored = dto.vpn_protocol;
        if (!string.IsNullOrWhiteSpace(stored))
        {
            var protocol = VpnProtocolExtensions.FromStorage(stored);
            if (protocol == VpnProtocol.Awg && !OsHelper.IsWindows10OrGreater())
                return VpnProtocol.Auto;
            return protocol;
        }

        if (!string.IsNullOrWhiteSpace(dto.legacy_protocol))
            return VpnProtocolExtensions.FromStorage(dto.legacy_protocol);

        return VpnProtocol.Auto;
    }

    public static (double left, double top)? GetWindowPosition()
    {
        try
        {
            if (!File.Exists(StoragePath))
                return null;
            var json = File.ReadAllText(StoragePath);
            var dto = JsonSerializer.Deserialize<PrefsDto>(json);
            if (dto?.window_left is not { } left || dto.window_top is not { } top)
                return null;
            return (left, top);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveWindowPosition(double left, double top)
    {
        try
        {
            var dto = File.Exists(StoragePath)
                ? JsonSerializer.Deserialize<PrefsDto>(File.ReadAllText(StoragePath)) ?? new PrefsDto()
                : new PrefsDto();
            dto.window_left = left;
            dto.window_top = top;
            var dir = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(dto,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            /* ignore */
        }
    }
}
