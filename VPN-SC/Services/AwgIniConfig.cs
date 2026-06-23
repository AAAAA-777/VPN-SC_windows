using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VpnSc.Services;

public sealed class AwgIniConfig
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);

    public static AwgIniConfig Parse(string ini)
    {
        var cfg = new AwgIniConfig();
        string? current = null;
        foreach (var raw in ini.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                current = line.Substring(1, line.Length - 2).Trim();
                cfg.Section(current);
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq <= 0 || current == null)
                continue;
            cfg.SetValue(current, line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
        }
        return cfg;
    }

    public Dictionary<string, string>? Section(string name)
    {
        if (!_sections.TryGetValue(name, out var sec))
        {
            sec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sections[name] = sec;
        }
        return sec;
    }

    public void SetValue(string section, string key, string value) => Section(section)![key] = value;

    public string ToIniText()
    {
        var sb = new StringBuilder();
        foreach (var pair in _sections)
        {
            if (pair.Value.Count == 0) continue;
            sb.Append('[').Append(pair.Key).AppendLine("]");
            foreach (var key in pair.Value.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                sb.Append(key).Append('=').AppendLine(pair.Value[key]);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}
