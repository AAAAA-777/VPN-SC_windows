using System.Text.Json;

namespace VpnSc.Services;

public sealed class AwgProfile
{
    public string ConfigIni { get; init; } = "";
    public string? Mtu { get; init; }
    public int? Port { get; init; }
    public string? ClientPrivateKey { get; init; }
    public Dictionary<string, string> Obfuscation { get; init; } = new();

    public bool IsAwg2
    {
        get
        {
            var keys = new HashSet<string>(Obfuscation.Keys.Select(k => k.ToUpperInvariant()));
            if (keys.Contains("S3") || keys.Contains("S4")) return true;
            foreach (var h in new[] { "H1", "H2", "H3", "H4" })
            {
                foreach (var k in Obfuscation.Keys)
                {
                    if (!string.Equals(k, h, StringComparison.OrdinalIgnoreCase)) continue;
                    if (Obfuscation[k].Contains("-")) return true;
                }
            }
            foreach (var i in new[] { "I1", "I2", "I3", "I4", "I5" })
                if (keys.Contains(i)) return true;
            return false;
        }
    }

    public static AwgProfile FromLastConfigJson(JsonElement lastConfig)
    {
        if (!lastConfig.TryGetProperty("config", out var configEl) || configEl.ValueKind != JsonValueKind.String)
            throw new FormatException("last_config missing config INI string");
        var config = configEl.GetString() ?? "";
        if (config.Length == 0) throw new FormatException("last_config config is empty");

        int? port = null;
        if (lastConfig.TryGetProperty("port", out var portEl))
        {
            if (portEl.ValueKind == JsonValueKind.Number && portEl.TryGetInt32(out var p)) port = p;
            else if (portEl.ValueKind == JsonValueKind.String && int.TryParse(portEl.GetString(), out var ps)) port = ps;
        }
        string? clientKey = lastConfig.TryGetProperty("client_priv_key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String
            ? keyEl.GetString() : null;
        string? mtu = lastConfig.TryGetProperty("mtu", out var mtuEl) ? mtuEl.ToString() : null;

        return new AwgProfile
        {
            ConfigIni = config,
            Mtu = mtu,
            Port = port,
            ClientPrivateKey = clientKey,
            Obfuscation = ExtractObfuscation(config)
        };
    }

    private static Dictionary<string, string> ExtractObfuscation(string ini)
    {
        var awgKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Jc","Jmin","Jmax","S1","S2","S3","S4","H1","H2","H3","H4","I1","I2","I3","I4","I5"
        };
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in ini.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith(";")) continue;
            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            var key = trimmed.Substring(0, eq).Trim();
            if (!awgKeys.Contains(key)) continue;
            result[key] = trimmed.Substring(eq + 1).Trim();
        }
        return result;
    }
}
