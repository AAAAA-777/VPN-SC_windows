using System.IO;
using System.Text.Json;
using VpnSc.Localization;

namespace VpnSc.Services;

public static class LanguageService
{
    private const string Key = "selected_language";
    private const string DefaultLang = "ru";

    private static string PrefsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VpnSecurityConnect",
            "language.json");

    public static async Task<string> GetSavedLanguageAsync()
    {
        try
        {
            if (!File.Exists(PrefsPath))
                return DefaultLang;
            using var fs = File.OpenRead(PrefsPath);
            var d = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(fs);
            return d != null && d.TryGetValue(Key, out var lang) ? lang : DefaultLang;
        }
        catch
        {
            return DefaultLang;
        }
    }

    public static async Task SaveLanguageAsync(string code)
    {
        var dir = Path.GetDirectoryName(PrefsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var d = new Dictionary<string, string> { [Key] = code };
        using var fs = File.Create(PrefsPath);
        await JsonSerializer.SerializeAsync(fs, d);
        I18n.SetLanguage(code);
    }
}

