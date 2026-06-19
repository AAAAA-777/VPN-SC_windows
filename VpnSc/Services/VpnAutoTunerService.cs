using System.Diagnostics;
using VpnSc.Helpers;
using VpnSc.Models;

namespace VpnSc.Services;

public sealed class VpnAutoTunerResult
{
    public FragmentationSettings FragmentationSettings { get; init; } = FragmentationSettings.Defaults;
    public string? FingerprintOverride { get; init; }
}

public static class VpnAutoTunerService
{
    public const string AutotuneFailedReason = "autotune_failed";

    private static readonly string[] TestUrls =
    {
        "https://www.gstatic.com/generate_204",
        "https://cp.cloudflare.com/generate_204",
        "https://www.google.com/generate_204"
    };

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    public static bool ShouldAutoTune(VpnService.ServerInfo server)
    {
        var security = string.IsNullOrEmpty(server.Security) ? "reality" : server.Security;
        if (security != "tls" && security != "reality")
            return false;
        return server.FinalMask == null || server.FinalMask.Count == 0;
    }

    public static async Task<VpnAutoTunerResult?> TuneAsync(
        VpnService.ServerInfo server,
        string userUuid,
        bool fingerprintAuto,
        bool fragmentationAuto,
        FragmentationSettings manualFragmentation)
    {
        if (!ShouldAutoTune(server))
        {
            return new VpnAutoTunerResult
            {
                FragmentationSettings = manualFragmentation,
                FingerprintOverride = await FragmentationSettingsService.GetFingerprintOverrideAsync()
            };
        }

        string? selectedFp = await FragmentationSettingsService.GetFingerprintOverrideAsync();
        var selectedFragmentation = manualFragmentation;

        if (fingerprintAuto && fragmentationAuto)
        {
            var combined = await TuneFingerprintAndFragmentationAsync(server, userUuid);
            if (combined == null)
                return null;
            selectedFp = combined.Value.Fingerprint;
            selectedFragmentation = combined.Value.Settings;
            await FragmentationSettingsService.SetFingerprintOverrideAsync(selectedFp);
            await FragmentationSettingsService.SaveAsync(selectedFragmentation);
            await FragmentationSettingsService.SetLastPresetIndexAsync(combined.Value.PresetIndex);
        }
        else
        {
            if (fingerprintAuto)
            {
                var fp = await TuneFingerprintAsync(
                    server,
                    userUuid,
                    manualFragmentation.Enabled ? manualFragmentation : FragmentationSettings.Defaults);
                if (fp == null)
                    return null;
                selectedFp = fp;
                await FragmentationSettingsService.SetFingerprintOverrideAsync(fp);
            }

            if (fragmentationAuto)
            {
                var lastIndex = await FragmentationSettingsService.GetLastPresetIndexAsync();
                var presets = FragmentationSettingsService.Presets;
                var order = BuildPresetOrder(lastIndex, presets.Count);
                FragmentationSettings? found = null;
                var foundIndex = lastIndex;

                foreach (var index in order)
                {
                    var preset = presets[index];
                    if (!preset.Settings.Enabled)
                        continue;
                    var ok = await TestConfigAsync(server, userUuid, preset.Settings, selectedFp);
                    if (!ok)
                        continue;
                    found = preset.Settings;
                    foundIndex = index;
                    break;
                }

                if (found == null)
                    return null;

                selectedFragmentation = found;
                await FragmentationSettingsService.SaveAsync(found);
                await FragmentationSettingsService.SetLastPresetIndexAsync(foundIndex);
            }
        }

        return new VpnAutoTunerResult
        {
            FragmentationSettings = selectedFragmentation,
            FingerprintOverride = selectedFp
        };
    }

    private static async Task<(string Fingerprint, FragmentationSettings Settings, int PresetIndex)?>
        TuneFingerprintAndFragmentationAsync(VpnService.ServerInfo server, string userUuid)
    {
        var presets = FragmentationSettingsService.Presets;
        var lastIndex = await FragmentationSettingsService.GetLastPresetIndexAsync();
        var presetOrder = BuildPresetOrder(lastIndex, presets.Count);

        foreach (var fp in FragmentationSettingsService.FingerprintCandidates)
        {
            foreach (var index in presetOrder)
            {
                var preset = presets[index];
                if (!preset.Settings.Enabled)
                    continue;
                var ok = await TestConfigAsync(server, userUuid, preset.Settings, fp);
                if (ok)
                    return (fp, preset.Settings, index);
            }
        }
        return null;
    }

    private static async Task<string?> TuneFingerprintAsync(
        VpnService.ServerInfo server,
        string userUuid,
        FragmentationSettings fragmentationSettings)
    {
        foreach (var fp in FragmentationSettingsService.FingerprintCandidates)
        {
            if (await TestConfigAsync(server, userUuid, fragmentationSettings, fp))
                return fp;
        }
        return null;
    }

    private static List<int> BuildPresetOrder(int lastIndex, int length)
    {
        var order = new List<int>();
        if (lastIndex >= 0 && lastIndex < length)
            order.Add(lastIndex);
        for (var i = 0; i < length; i++)
        {
            if (!order.Contains(i))
                order.Add(i);
        }
        return order;
    }

    private static async Task<bool> TestConfigAsync(
        VpnService.ServerInfo server,
        string userUuid,
        FragmentationSettings fragmentationSettings,
        string? fingerprintOverride)
    {
        await HiddenProcessService.StopVpnProcessesAsync();
        await Task.Delay(500);

        var config = XrayConfigBuilder.Build(server, userUuid, fragmentationSettings, fingerprintOverride);
        var xrayPath = FileManagerService.GetXrayPath();
        if (!File.Exists(xrayPath))
            return false;

        var configPath = await XrayConfigBuilder.WriteConfigFileAsync(config);
        if (!HiddenProcessService.StartHiddenProcess(xrayPath, "run", "-config", configPath))
            return false;

        await Task.Delay(TimeSpan.FromSeconds(3));
        var ok = await MeasureDelayAsync();
        await HiddenProcessService.StopVpnProcessesAsync();

        try
        {
            if (File.Exists(configPath))
                File.Delete(configPath);
        }
        catch
        {
            /* ignore */
        }

        return ok;
    }

    public static async Task<bool> MeasureDelayAsync()
    {
        for (var i = 0; i < TestUrls.Length; i++)
        {
            if (await ProbeUrlAsync(TestUrls[i]))
                return true;
        }
        return false;
    }

    private static async Task<bool> ProbeUrlAsync(string url)
    {
        try
        {
            var curl = ResolveCurlExecutable();
            var psi = new ProcessStartInfo
            {
                FileName = curl,
                Arguments =
                    $"--socks5-hostname 127.0.0.1:1080 --max-time {TestTimeout.TotalSeconds:0} -s -o NUL -w %{{http_code}} \"{url}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return false;
            using var cts = new CancellationTokenSource(TestTimeout);
            try
            {
                await ProcessCompat.WaitForExitAsync(process, cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                        ProcessCompat.Kill(process);
                }
                catch
                {
                    /* ignore */
                }
                return false;
            }

            var code = (await process.StandardOutput.ReadToEndAsync()).Trim();
            return code == "204";
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveCurlExecutable()
    {
        const string systemCurl = @"C:\Windows\System32\curl.exe";
        return File.Exists(systemCurl) ? systemCurl : "curl";
    }
}
