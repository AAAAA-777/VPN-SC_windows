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
        var raw = DecodeBase64UrlPayload(vpnUri);
        if (raw.Length == 0)
            throw new FormatException("vpn:// payload is empty");

        var decompressed = DecompressQtPayload(raw);
        return Encoding.UTF8.GetString(decompressed);
    }

    public static JsonDocument DecodeToDocument(string vpnUri) =>
        JsonDocument.Parse(DecodeToJson(vpnUri));

    private static string StripVpnPrefix(string vpnUri)
    {
        var payload = vpnUri.Trim();
        if (payload.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
            payload = payload.Substring(6);
        return payload;
    }

    private static byte[] DecodeBase64UrlPayload(string vpnUri)
    {
        var payload = StripVpnPrefix(vpnUri);
        var padded = PadBase64Url(payload);
        var b64 = padded.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64);
    }

    private static byte[] DecompressQtPayload(byte[] raw)
    {
        if (raw.Length < 4)
            throw new FormatException("vpn:// payload too short");

        // Amnezia uses Qt qCompress: 4-byte BE size header + zlib stream.
        var attempts = new (int offset, int count, bool nowrap, string label)[]
        {
            (4, raw.Length - 4, false, "qt-zlib"),
            (0, raw.Length, false, "zlib"),
            (4, raw.Length - 4, true, "qt-raw-deflate"),
        };

        Exception? lastError = null;
        foreach (var (offset, count, nowrap, _) in attempts)
        {
            if (count <= 0)
                continue;
            try
            {
                return Inflate(raw, offset, count, nowrap);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        var detail = lastError?.Message ?? "unknown";
        throw new FormatException("Failed to decompress vpn:// config: " + detail, lastError);
    }

    private static byte[] Inflate(byte[] raw, int offset, int count, bool nowrap)
    {
        using var compressed = new MemoryStream(raw, offset, count, writable: false);
        using var zlib = new InflaterInputStream(compressed, new Inflater(nowrap));
        using var outMs = new MemoryStream();
        zlib.CopyTo(outMs);
        return outMs.ToArray();
    }

    private static string PadBase64Url(string s)
    {
        var r = s.Length % 4;
        return r == 0 ? s : s + new string('=', 4 - r);
    }
}
