using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using VpnSc.Helpers;
using VpnSc.Models;

namespace VpnSc.Services;

public static class VpnService
{
    public const int MaxServerFallbackAttempts = 5;

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VPN-SC APP");
        return c;
    }

    public sealed class ServerInfo
    {
        public string Name { get; init; } = "";
        public string Host { get; init; } = "";
        public int Port { get; init; }
        public string Id { get; init; } = "";
        public string Type { get; init; } = "tcp";
        public string Security { get; init; } = "reality";
        public string Pbk { get; init; } = "";
        public string Fp { get; init; } = "chrome";
        public string Sni { get; init; } = "";
        public string Sid { get; init; } = "";
        public string Spx { get; init; } = "/";
        public string Flow { get; init; } = "";
        public string Path { get; init; } = "";
        public string HostHeader { get; init; } = "";
        public string ServiceName { get; init; } = "";
        public string Authority { get; init; } = "";
        public JsonObject? FinalMask { get; init; }
    }

    public sealed class StartVpnResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public string? Reason { get; init; }
        public string? ConnectedServer { get; init; }
    }

    public static async Task<(bool ok, List<string> servers, string? error)> GetServersAsync(string uuid)
    {
        try
        {
            var text = await FetchConnectFeedAsync(uuid);
            var servers = ParseVpnConfig(text);
            return (true, servers.Select(s => s.Name).ToList(), null);
        }
        catch (Exception ex)
        {
            return (false, new List<string>(), ex.Message);
        }
    }

    public static async Task<StartVpnResult> StartVpnAsync(
        string uuid,
        string serverName,
        bool abortOnAutotuneFail = false)
    {
        try
        {
            await VpnModeSwitch.StopAwgAsync();

            if (!FileManagerService.CheckRequiredFiles())
                return Fail("xray.exe не найден. Загрузите файлы VPN.");

            var text = await FetchConnectFeedAsync(uuid);
            var servers = ParseVpnConfig(text);
            if (servers.Count == 0)
                return Fail("Сервер не найден");

            var selected = servers.FirstOrDefault(s => s.Name == serverName) ?? servers[0];
            var tried = new HashSet<string>(StringComparer.Ordinal);
            string? lastError = null;

            async Task<StartVpnResult> TryServerAsync(ServerInfo server)
            {
                tried.Add(server.Name);
                var result = await ConnectToServerAsync(uuid, server, abortOnAutotuneFail);
                if (result.Success)
                {
                    return new StartVpnResult
                    {
                        Success = true,
                        ConnectedServer = server.Name
                    };
                }

                lastError = result.Error;
                await CleanupFailedConnectionAsync();
                return result;
            }

            var primary = await TryServerAsync(selected);
            if (primary.Success)
                return primary;
            if (primary.Reason == VpnAutoTunerService.AutotuneFailedReason)
                return primary;

            var fallbackLimit = Math.Min(MaxServerFallbackAttempts, servers.Count);
            for (var i = 0; i < fallbackLimit; i++)
            {
                var server = servers[i];
                if (tried.Contains(server.Name))
                    continue;
                var result = await TryServerAsync(server);
                if (result.Success)
                    return result;
                if (result.Reason == VpnAutoTunerService.AutotuneFailedReason)
                    return result;
            }

            return Fail(lastError ?? "Не удалось подключиться к VPN");
        }
        catch (Exception ex)
        {
            await CleanupFailedConnectionAsync();
            return Fail(ex.Message);
        }
    }

    public static bool IsVpnRunning()
    {
        try
        {
            var xrayPath = FileManagerService.GetXrayPath();
            var name = Path.GetFileNameWithoutExtension(xrayPath);
            return Process.GetProcessesByName(name).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<(bool ok, string? error)> StopVpnAsync()
    {
        try
        {
            await HiddenProcessService.StopVpnProcessesAsync();
            SystemProxyService.DisableSystemProxy();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool ok, bool connected, JsonObject? stats)> GetVpnStatusAsync(string _)
    {
        try
        {
            if (!await IsXrayRunningAsync())
                return (true, false, null);
            var xray = FileManagerService.GetXrayPath();
            if (!File.Exists(xray))
                return (true, false, null);
            var uplink = await RunXrayStatAsync(xray,
                "inbound>>>socks-inbound>>>traffic>>>uplink");
            var downlink = await RunXrayStatAsync(xray,
                "outbound>>>proxy>>>traffic>>>downlink");
            if (uplink >= 0 || downlink >= 0)
            {
                var stats = new JsonObject
                {
                    ["uplink"] = JsonValue.Create(uplink),
                    ["downlink"] = JsonValue.Create(downlink)
                };
                return (true, true, stats);
            }
            return (true, true, null);
        }
        catch
        {
            return (false, false, null);
        }
    }

    public static async Task<string?> GetAwgVpnUriFromFeedAsync(string uuid)
    {
        try
        {
            var text = await FetchConnectFeedAsync(uuid);
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase))
                    return line;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string ResolveServerName(string? selected, IReadOnlyList<string> servers)
    {
        if (servers.Count == 0)
            return "";
        if (string.IsNullOrWhiteSpace(selected))
            return servers[0];
        return servers.FirstOrDefault(s => string.Equals(s, selected, StringComparison.Ordinal))
               ?? servers[0];
    }

    private static async Task<StartVpnResult> ConnectToServerAsync(
        string uuid,
        ServerInfo server,
        bool abortOnAutotuneFail)
    {
        var fragmentationSettings = await FragmentationSettingsService.LoadAsync();
        var fingerprintOverride = await FragmentationSettingsService.GetFingerprintOverrideAsync();
        var fingerprintAuto = await FragmentationSettingsService.IsFingerprintAutoEnabledAsync();
        var fragmentationAuto = await FragmentationSettingsService.IsAutoTuneEnabledAsync();

        if (VpnAutoTunerService.ShouldAutoTune(server) && (fingerprintAuto || fragmentationAuto))
        {
            var tuned = await VpnAutoTunerService.TuneAsync(
                server,
                uuid,
                fingerprintAuto,
                fragmentationAuto,
                fragmentationSettings);
            if (tuned == null)
            {
                if (abortOnAutotuneFail)
                {
                    return new StartVpnResult
                    {
                        Success = false,
                        Reason = VpnAutoTunerService.AutotuneFailedReason,
                        Error = VpnAutoTunerService.AutotuneFailedReason
                    };
                }
            }
            else
            {
                fragmentationSettings = tuned.FragmentationSettings;
                fingerprintOverride = tuned.FingerprintOverride;
            }
        }

        return await AttemptConnectAsync(
            uuid,
            server,
            fragmentationSettings,
            fingerprintOverride);
    }

    private static async Task<StartVpnResult> AttemptConnectAsync(
        string uuid,
        ServerInfo server,
        FragmentationSettings fragmentationSettings,
        string? fingerprintOverride)
    {
        var configPath = await CreateConfigFileAsync(
            server,
            uuid,
            fragmentationSettings,
            fingerprintOverride);

        var xrayPath = FileManagerService.GetXrayPath();
        if (!File.Exists(xrayPath))
            return Fail("xray.exe не найден. Загрузите файлы VPN.");

        if (!HiddenProcessService.StartHiddenProcess(xrayPath, "run", "-config", configPath))
            return Fail("Не удалось запустить xray.exe");

        await Task.Delay(TimeSpan.FromSeconds(3));
        if (!await IsXrayRunningAsync())
            return Fail("xray.exe не запустился");

        SystemProxyService.EnableSystemProxy("127.0.0.1", 1080);
        var status = await GetVpnStatusAsync(uuid);
        if (status.ok && status.connected)
        {
            _ = ScheduleConfigDeletionAsync(configPath);
            return new StartVpnResult { Success = true };
        }

        return Fail("Не удалось подключиться к VPN");
    }

    private static async Task CleanupFailedConnectionAsync()
    {
        await HiddenProcessService.StopVpnProcessesAsync();
        SystemProxyService.DisableSystemProxy();
    }

    private static StartVpnResult Fail(string? error) => new()
    {
        Success = false,
        Error = error
    };

    private static async Task<string> FetchConnectFeedAsync(string uuid)
    {
        var url = $"https://connect.vpn-sc.com/?id={Uri.EscapeDataString(uuid)}";
        return await Http.GetStringAsync(url);
    }

    private static async Task<long> RunXrayStatAsync(string xrayPath, string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = xrayPath,
                Arguments = "api stats --server=127.0.0.1:10085 --name " + name,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null)
                return -1;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var stdoutTask = Task.Run(() => p.StandardOutput.ReadToEnd(), cts.Token);
            try
            {
                await ProcessCompat.WaitForExitAsync(p, cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!p.HasExited)
                        ProcessCompat.Kill(p);
                }
                catch
                {
                    /* ignore */
                }
                return -1;
            }
            var stdout = await stdoutTask;
            if (string.IsNullOrWhiteSpace(stdout))
                return -1;
            using var doc = JsonDocument.Parse(stdout);
            if (doc.RootElement.TryGetProperty("stat", out var stat) &&
                stat.TryGetProperty("value", out var val))
                return val.GetInt64();
        }
        catch
        {
            /* ignore */
        }
        return -1;
    }

    private static async Task<bool> IsXrayRunningAsync() =>
        await HiddenProcessService.CheckVpnProcessesAsync(FileManagerService.GetXrayPath());

    private static async Task ScheduleConfigDeletionAsync(string configPath)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (File.Exists(configPath))
                File.Delete(configPath);
        }
        catch
        {
            /* ignore */
        }
    }

    private static async Task<string> CreateConfigFileAsync(
        ServerInfo server,
        string userUuid,
        FragmentationSettings fragmentationSettings,
        string? fingerprintOverride)
    {
        var config = XrayConfigBuilder.Build(server, userUuid, fragmentationSettings, fingerprintOverride);
        return await XrayConfigBuilder.WriteConfigFileAsync(config);
    }

    private static List<ServerInfo> ParseVpnConfig(string configText)
    {
        var list = new List<ServerInfo>();
        foreach (var rawLine in configText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || !line.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                if (!Uri.TryCreate(line, UriKind.Absolute, out var uri))
                    continue;
                var name = string.IsNullOrEmpty(uri.Fragment)
                    ? "Сервер"
                    : Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));
                var uuid = uri.UserInfo;
                if (string.IsNullOrEmpty(uuid))
                    continue;
                var q = ParseQuery(uri.Query);
                list.Add(new ServerInfo
                {
                    Name = name,
                    Host = uri.Host,
                    Port = uri.Port > 0 ? uri.Port : 443,
                    Id = uuid,
                    Type = GetQ(q, "type") ?? "tcp",
                    Security = GetQ(q, "security") ?? "reality",
                    Pbk = GetQ(q, "pbk") ?? "",
                    Fp = GetQ(q, "fp") ?? "chrome",
                    Sni = GetQ(q, "sni") ?? "",
                    Sid = GetQ(q, "sid") ?? "",
                    Spx = GetQ(q, "spx") ?? "/",
                    Flow = GetQ(q, "flow") ?? "",
                    Path = GetQ(q, "path") ?? "",
                    HostHeader = GetQ(q, "host") ?? "",
                    ServiceName = GetQ(q, "serviceName") ?? "",
                    Authority = GetQ(q, "authority") ?? "",
                    FinalMask = XrayConfigBuilder.ParseFinalMaskFromQuery(GetQ(q, "fm"))
                });
            }
            catch
            {
                /* skip bad line */
            }
        }
        return list;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var q = query.TrimStart('?');
        foreach (var part in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var k = Uri.UnescapeDataString(eq < 0 ? part : part.Substring(0, eq));
            var v = eq < 0 ? "" : Uri.UnescapeDataString(part.Substring(eq + 1));
            d[k] = v;
        }
        return d;
    }

    private static string? GetQ(Dictionary<string, string> q, string key) =>
        q.TryGetValue(key, out var v) ? v : null;
}
