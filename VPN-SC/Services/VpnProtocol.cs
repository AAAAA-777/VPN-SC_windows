namespace VpnSc.Services;

public enum VpnProtocol
{
    Stealth,
    Awg,
    Auto
}

public static class VpnProtocolExtensions
{
    public const string AutoDisplayName = "Auto";

    public static string StorageValue(this VpnProtocol protocol) => protocol switch
    {
        VpnProtocol.Awg => "amneziawg",
        VpnProtocol.Auto => "auto",
        _ => "stealth"
    };

    public static VpnProtocol FromStorage(string? value)
    {
        if (value is not { Length: > 0 })
            return VpnProtocol.Auto;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "auto" => VpnProtocol.Auto,
            "stealth" or "xray" => VpnProtocol.Stealth,
            "amneziawg" or "awg" or "amnezia wg" or "amneziawg 2.0" => VpnProtocol.Awg,
            _ when normalized.Contains("amnezia") || normalized.Contains("awg") => VpnProtocol.Awg,
            _ when normalized.Contains("stealth") => VpnProtocol.Stealth,
            _ => VpnProtocol.Auto
        };
    }

    public static string LegacyLabel(VpnProtocol protocol) => protocol switch
    {
        VpnProtocol.Awg => "Stealth (WG)",
        VpnProtocol.Auto => AutoDisplayName,
        _ => "Stealth"
    };
}
