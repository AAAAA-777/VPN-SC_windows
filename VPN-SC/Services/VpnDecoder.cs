using System.IO;
using System.Text;
using System.Text.Json;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace VpnSc.Services;

public static class VpnDecoder
{
    public static string DecodeToJson(string vpnUri)
    {
        var payload = vpnUri.Trim();
        if (payload.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
            payload = payload.Substring(6);

        var padded = PadBase64Url(payload);
        var b64 = padded.Replace('-', '+').Replace('_', '/');
        var raw = Convert.FromBase64String(b64);
        if (raw.Length < 5)
            throw new FormatException("vpn:// payload too short");

        using var compressed = new MemoryStream(raw, 4, raw.Length - 4);
        using var zlib = new InflaterInputStream(compressed, new Inflater(true));
        using var outMs = new MemoryStream();
        zlib.CopyTo(outMs);
        return Encoding.UTF8.GetString(outMs.ToArray());
    }

    public static JsonDocument DecodeToDocument(string vpnUri) =>
        JsonDocument.Parse(DecodeToJson(vpnUri));

    private static string PadBase64Url(string s)
    {
        var r = s.Length % 4;
        return r == 0 ? s : s + new string('=', 4 - r);
    }
}
