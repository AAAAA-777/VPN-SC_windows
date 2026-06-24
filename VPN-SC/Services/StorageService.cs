using VpnSc.Helpers;
using System.IO;
using System.Text.Json;

namespace VpnSc.Services;

public static class StorageService
{
    private static readonly object PrefsLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

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

    public static Task<PrefsDto> LoadPrefsAsync() => LoadAsync();

    public static Task SavePrefsAsync(PrefsDto dto) => SaveAsync(dto);

    public static Task UpdatePrefsAsync(Action<PrefsDto> update)
    {
        if (update == null)
            throw new ArgumentNullException(nameof(update));

        lock (PrefsLock)
        {
            var dto = TryLoadUnsafe();
            update(dto);
            SaveUnsafe(dto);
        }

        return Task.CompletedTask;
    }

    private static Task<PrefsDto> LoadAsync()
    {
        lock (PrefsLock)
            return Task.FromResult(TryLoadUnsafe());
    }

    private static Task SaveAsync(PrefsDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        lock (PrefsLock)
            SaveUnsafe(dto);

        return Task.CompletedTask;
    }

    private static PrefsDto TryLoadUnsafe()
    {
        try
        {
            return LoadUnsafe();
        }
        catch
        {
            return new PrefsDto();
        }
    }

    private static PrefsDto LoadUnsafe()
    {
        if (!File.Exists(StoragePath))
            return new PrefsDto();

        var json = File.ReadAllText(StoragePath);
        if (string.IsNullOrWhiteSpace(json))
            return new PrefsDto();

        return JsonSerializer.Deserialize<PrefsDto>(json) ?? new PrefsDto();
    }

    private static void SaveUnsafe(PrefsDto dto)
    {
        var dir = Path.GetDirectoryName(StoragePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = StoragePath + ".tmp";
        var backupPath = StoragePath + ".bak";
        var payload = JsonSerializer.Serialize(dto, JsonOptions);

        File.WriteAllText(tempPath, payload, FileCompat.Utf8NoBom);

        try
        {
            if (File.Exists(StoragePath))
            {
                try
                {
                    File.Replace(tempPath, StoragePath, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(tempPath, StoragePath, true);
                }
                catch (IOException)
                {
                    File.Copy(tempPath, StoragePath, true);
                }
            }
            else
            {
                File.Move(tempPath, StoragePath);
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
            TryDeleteFile(backupPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }

    public static Task MigrateUnencryptedDataAsync()
    {
        lock (PrefsLock)
        {
            var dto = TryLoadUnsafe();
            var changed = false;

            if (dto.access_token is { Length: > 0 } accessToken && EncryptionService.NeedsEncryption(accessToken))
            {
                var plain = EncryptionService.Decrypt(accessToken);
                if (!string.IsNullOrEmpty(plain))
                {
                    dto.access_token = EncryptionService.Encrypt(plain);
                    changed = true;
                }
            }

            if (dto.user_data is { Length: > 0 } userData && EncryptionService.NeedsEncryption(userData))
            {
                var plain = EncryptionService.Decrypt(userData);
                if (!string.IsNullOrEmpty(plain))
                {
                    try
                    {
                        _ = JsonSerializer.Deserialize<JsonElement>(plain);
                        dto.user_data = EncryptionService.Encrypt(plain);
                        changed = true;
                    }
                    catch
                    {
                        /* ignore invalid legacy payload */
                    }
                }
            }

            if (changed)
                SaveUnsafe(dto);
        }

        return Task.CompletedTask;
    }

    public static Task SaveAccessTokenAsync(string token) =>
        UpdatePrefsAsync(dto => dto.access_token = EncryptionService.Encrypt(token));

    public static async Task<string?> GetAccessTokenAsync()
    {
        var dto = await LoadAsync();
        if (dto.access_token is not { Length: > 0 } encryptedToken)
            return null;
        try
        {
            var token = EncryptionService.Decrypt(encryptedToken);
            if (string.IsNullOrEmpty(token) || EncryptionService.IsDpapiProtected(token))
                return null;
            return token;
        }
        catch
        {
            return null;
        }
    }

    public static Task SaveUserDataAsync(JsonElement userData)
    {
        var json = JsonSerializer.Serialize(userData);
        return UpdatePrefsAsync(dto => dto.user_data = EncryptionService.Encrypt(json));
    }

    public static async Task<JsonElement?> GetUserDataAsync()
    {
        var dto = await LoadAsync();
        if (dto.user_data is not { Length: > 0 } encryptedUserData)
            return null;
        try
        {
            var decrypted = EncryptionService.Decrypt(encryptedUserData);
            if (string.IsNullOrEmpty(decrypted) || EncryptionService.IsDpapiProtected(decrypted))
                return null;
            return JsonSerializer.Deserialize<JsonElement>(decrypted);
        }
        catch
        {
            return null;
        }
    }

    public static Task SetLoggedInAsync(bool value) =>
        UpdatePrefsAsync(dto => dto.is_logged_in = value);

    public static async Task<bool> IsLoggedInAsync()
    {
        var dto = await LoadAsync();
        return dto.is_logged_in == true;
    }

    public static Task ClearAllAsync()
    {
        lock (PrefsLock)
        {
            var dto = TryLoadUnsafe();
            var cleared = new PrefsDto
            {
                window_left = dto.window_left,
                window_top = dto.window_top
            };
            SaveUnsafe(cleared);
        }

        return Task.CompletedTask;
    }

    public static async Task ClearAllWithLogoutAsync()
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (token is { Length: > 0 })
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

    public static Task SaveVpnProtocolAsync(VpnProtocol protocol) =>
        UpdatePrefsAsync(dto =>
        {
            dto.vpn_protocol = protocol.StorageValue();
            dto.legacy_protocol = VpnProtocolExtensions.LegacyLabel(protocol);
        });

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
        lock (PrefsLock)
        {
            try
            {
                var dto = TryLoadUnsafe();
                if (dto.window_left is not { } left || dto.window_top is not { } top)
                    return null;
                return (left, top);
            }
            catch
            {
                return null;
            }
        }
    }

    public static void SaveWindowPosition(double left, double top)
    {
        lock (PrefsLock)
        {
            try
            {
                var dto = TryLoadUnsafe();
                dto.window_left = left;
                dto.window_top = top;
                SaveUnsafe(dto);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
