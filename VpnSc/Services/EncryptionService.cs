using System.Security.Cryptography;
using System.Text;

namespace VpnSc.Services;

public static class EncryptionService
{
    private static string GetDeviceKey()
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

    public static string Encrypt(string plainText)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(GetDeviceKey());
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = new byte[plainBytes.Length];
            for (var i = 0; i < plainBytes.Length; i++)
                encrypted[i] = (byte)(plainBytes[i] ^ key[i % key.Length]);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return plainText;
        }
    }

    public static string Decrypt(string encryptedText)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(GetDeviceKey());
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var decrypted = new byte[encryptedBytes.Length];
            for (var i = 0; i < encryptedBytes.Length; i++)
                decrypted[i] = (byte)(encryptedBytes[i] ^ key[i % key.Length]);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return encryptedText;
        }
    }

    public static bool IsEncrypted(string text)
    {
        try
        {
            Convert.FromBase64String(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

