using System.Security.Cryptography;
using System.Text;

namespace VpnSc.Services;

public static class EncryptionService
{
    private const string DpapiPrefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VpnSecurityConnect.v1");

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            return DpapiPrefix + Convert.ToBase64String(protectedBytes);
        }
        catch
        {
            return plainText;
        }
    }

    public static string Decrypt(string storedText)
    {
        if (string.IsNullOrEmpty(storedText))
            return storedText;

        if (storedText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            var dpapi = TryUnprotectDpapi(storedText);
            return dpapi ?? string.Empty;
        }

        var legacy = TryDecryptLegacyXor(storedText);
        if (legacy != null)
            return legacy;

        return storedText;
    }

    public static bool IsDpapiProtected(string text) =>
        text.StartsWith(DpapiPrefix, StringComparison.Ordinal);

    public static bool NeedsEncryption(string text) =>
        !string.IsNullOrEmpty(text) && !IsDpapiProtected(text);

    private static string? TryUnprotectDpapi(string storedText)
    {
        try
        {
            var payload = storedText.Substring(DpapiPrefix.Length);
            var protectedBytes = Convert.FromBase64String(payload);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryDecryptLegacyXor(string encryptedText)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(GetLegacyDeviceKey());
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            if (encryptedBytes.Length == 0)
                return null;

            var decrypted = new byte[encryptedBytes.Length];
            for (var i = 0; i < encryptedBytes.Length; i++)
                decrypted[i] = (byte)(encryptedBytes[i] ^ key[i % key.Length]);

            var text = Encoding.UTF8.GetString(decrypted);
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static string GetLegacyDeviceKey()
    {
        var computer = Environment.GetEnvironmentVariable("COMPUTERNAME")
                       ?? Environment.MachineName;
        var user = Environment.GetEnvironmentVariable("USERNAME")
                   ?? Environment.UserName;
        var systemInfo = $"{computer}-{user}";
        if (string.IsNullOrEmpty(systemInfo.Trim('-')))
            systemInfo = "default_device";

        var raw = $"VPN-SC-{systemInfo}-2025";
        byte[] hash;
        using (var md5 = MD5.Create())
            hash = md5.ComputeHash(Encoding.UTF8.GetBytes(raw));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return hex.Length >= 32 ? hex.Substring(0, 32) : hex.PadRight(32, '0');
    }
}
