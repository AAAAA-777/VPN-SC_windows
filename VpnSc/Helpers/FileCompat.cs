using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VpnSc.Helpers;

internal static class FileCompat
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static Task<string> ReadAllTextAsync(string path) =>
        Task.Run(() => File.ReadAllText(path));

    public static Task WriteAllTextAsync(string path, string contents, Encoding encoding)
    {
        return Task.Run(() => File.WriteAllText(path, contents, encoding));
    }

    public static Task WriteAllBytesAsync(string path, byte[] bytes)
    {
        return Task.Run(() => File.WriteAllBytes(path, bytes));
    }
}
