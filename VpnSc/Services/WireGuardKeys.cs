using System.Security.Cryptography;

namespace VpnSc.Services;

public static class WireGuardKeys
{
    public static string GeneratePrivateKeyBase64()
    {
        var privateBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(privateBytes);
        privateBytes[0] &= 248;
        privateBytes[31] &= 127;
        privateBytes[31] |= 64;
        return Convert.ToBase64String(privateBytes).TrimEnd('=');
    }
}
