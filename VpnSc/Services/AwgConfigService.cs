using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class AwgConfigService
{
    public sealed class ImportResult
    {
        public string ConfIni { get; init; } = "";
        public AwgProfile Profile { get; init; } = new();
        public string PrivateKeyBase64 { get; init; } = "";
        public bool IsAwg2 => Profile.IsAwg2;
    }

    public static async Task<ImportResult> ImportVpnUriAsync(string vpnUri, string? privateKeyBase64 = null)
    {
        using var doc = VpnDecoder.DecodeToDocument(vpnUri);
        var root = doc.RootElement;
        var amnezia = AmneziaConfig.Parse(root);

        AwgProfile profile;
        string privateKey;

        if (amnezia.IsApiKey)
        {
            profile = await AmneziaApiClient.FetchProfileAsync(amnezia);
            privateKey = profile.ClientPrivateKey ?? privateKeyBase64 ?? WireGuardKeys.GeneratePrivateKeyBase64();
        }
        else
        {
            profile = ProfileFromAmnezia(amnezia);
            privateKey = privateKeyBase64 ?? profile.ClientPrivateKey ?? await LoadOrCreatePersistedKeyAsync(vpnUri);
        }

        var conf = Build(profile, privateKey, amnezia.Dns1, amnezia.Dns2);
        return new ImportResult { ConfIni = conf, Profile = profile, PrivateKeyBase64 = privateKey };
    }

    public static AwgProfile ProfileFromAmnezia(AmneziaConfig amnezia)
    {
        if (!amnezia.AwgSection.HasValue)
            throw new FormatException("No AWG container in Amnezia config");
        var awg = amnezia.AwgSection.Value;
        if (!awg.TryGetProperty("last_config", out var lastRaw))
            throw new FormatException("AWG container missing last_config");

        JsonElement lastConfig;
        if (lastRaw.ValueKind == JsonValueKind.String)
            lastConfig = JsonDocument.Parse(lastRaw.GetString() ?? "{}").RootElement;
        else
            lastConfig = lastRaw;

        return AwgProfile.FromLastConfigJson(lastConfig);
    }

    public static string Build(AwgProfile profile, string privateKeyBase64, string? primaryDns, string? secondaryDns)
    {
        var ini = profile.ConfigIni;
        ini = ini.Replace("$WIREGUARD_CLIENT_PRIVATE_KEY", privateKeyBase64);
        if (primaryDns != null) ini = ini.Replace("$PRIMARY_DNS", primaryDns);
        if (secondaryDns != null) ini = ini.Replace("$SECONDARY_DNS", secondaryDns);

        var config = AwgIniConfig.Parse(ini);
        config.SetValue("Interface", "PrivateKey", privateKeyBase64);
        if (!string.IsNullOrEmpty(profile.Mtu))
            config.SetValue("Interface", "MTU", profile.Mtu);
        config.Section("Interface")?.Remove("ListenPort");

        SanitizeInterface(config);
        return config.ToIniText();
    }

    private static void SanitizeInterface(AwgIniConfig config)
    {
        var iface = config.Section("Interface");
        if (iface == null) return;
        foreach (var key in iface.Keys.ToList())
        {
            var value = iface[key];
            if (string.IsNullOrEmpty(value)) { iface.Remove(key); continue; }
            if (value.Contains("<") && !value.StartsWith("\""))
                iface[key] = "\"" + value + "\"";
        }
    }

    private static async Task<string> LoadOrCreatePersistedKeyAsync(string vpnUri)
    {
        var id = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(vpnUri))).Replace("-", "").ToLowerInvariant();
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "awg_config", "keys");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, id + ".key");
        if (File.Exists(file))
            return (await Task.Run(() => File.ReadAllText(file))).Trim();
        var key = WireGuardKeys.GeneratePrivateKeyBase64();
        await FileCompat.WriteAllTextAsync(file, key, FileCompat.Utf8NoBom);
        return key;
    }
}

public sealed class AmneziaConfig
{
    public JsonElement Raw { get; init; }
    public bool IsApiKey { get; init; }
    public string? ApiEndpoint { get; init; }
    public string? ApiKey { get; init; }
    public string? Dns1 { get; init; }
    public string? Dns2 { get; init; }
    public List<JsonElement> Containers { get; init; } = new();

    public static AmneziaConfig Parse(JsonElement map)
    {
        string? apiEndpoint = map.TryGetProperty("api_endpoint", out var ep) ? ep.GetString() : null;
        string? apiKey = map.TryGetProperty("api_key", out var ak) ? ak.GetString() : null;
        var isApiKey = !string.IsNullOrEmpty(apiEndpoint);
        var containers = new List<JsonElement>();
        if (map.TryGetProperty("containers", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                containers.Add(item.Clone());
        }
        return new AmneziaConfig
        {
            Raw = map.Clone(),
            IsApiKey = isApiKey,
            ApiEndpoint = apiEndpoint,
            ApiKey = apiKey,
            Dns1 = map.TryGetProperty("dns1", out var d1) ? d1.GetString() : null,
            Dns2 = map.TryGetProperty("dns2", out var d2) ? d2.GetString() : null,
            Containers = containers
        };
    }

    public JsonElement? AwgSection
    {
        get
        {
            JsonElement? found = null;
            foreach (var c in Containers)
            {
                if (c.TryGetProperty("awg", out var awg))
                    found = awg;
            }
            return found;
        }
    }
}

public static class AmneziaApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static async Task<AwgProfile> FetchProfileAsync(AmneziaConfig apiConfig)
    {
        if (!apiConfig.IsApiKey)
            throw new ArgumentException("Expected API-key Amnezia config");
        var endpoint = apiConfig.ApiEndpoint ?? throw new ArgumentException("api_endpoint required");
        var apiKey = apiConfig.ApiKey ?? throw new ArgumentException("api_key required");

        var privateKey = WireGuardKeys.GeneratePrivateKeyBase64();
        var publicKey = DerivePublicKey(privateKey);
        var body = JsonSerializer.Serialize(new
        {
            public_key = publicKey,
            os_version = "windows",
            app_version = "4.8.12.9",
            uuid = Guid.NewGuid().ToString()
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.TryAddWithoutValidation("Authorization", "Api-Key " + apiKey);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("API request failed: HTTP " + (int)resp.StatusCode + " " + text);

        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("config", out var cfg) || cfg.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("Response missing config field");

        var vpnUri = cfg.GetString() ?? "";
        if (!vpnUri.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
            vpnUri = "vpn://" + vpnUri;
        using var inner = VpnDecoder.DecodeToDocument(vpnUri);
        var profile = AwgConfigService.ProfileFromAmnezia(AmneziaConfig.Parse(inner.RootElement));
        return new AwgProfile
        {
            ConfigIni = profile.ConfigIni,
            Mtu = profile.Mtu,
            Port = profile.Port,
            ClientPrivateKey = privateKey,
            Obfuscation = profile.Obfuscation
        };
    }

    private static string DerivePublicKey(string privateKeyBase64)
    {
        var padded = privateKeyBase64;
        var r = padded.Length % 4;
        if (r != 0) padded += new string('=', 4 - r);
        var privateBytes = Convert.FromBase64String(padded);
        var privateParams = new Org.BouncyCastle.Crypto.Parameters.X25519PrivateKeyParameters(privateBytes, 0);
        var publicParams = privateParams.GeneratePublicKey();
        return Convert.ToBase64String(publicParams.GetEncoded()).TrimEnd('=');
    }
}
