using VpnSc.Models;

namespace VpnSc.Services;

public static class FragmentationSettingsService
{
    public static readonly IReadOnlyList<FragmentationPreset> Presets = BuildPresets();

    private static IReadOnlyList<FragmentationPreset> BuildPresets()
    {
        var presets = new List<FragmentationPreset>
        {
            new("default", FragmentationSettings.Defaults)
        };

        // FragmentationPreset(
        //   'aggressive',
        //   FragmentationSettings(
        //     enabled: true,
        //     packets: 'tlshello',
        //     length: '1-4',
        //     interval: '10-20',
        //     maxSplit: '5-10',
        //   ),
        // ),
        // FragmentationPreset(
        //   'medium',
        //   FragmentationSettings(
        //     enabled: true,
        //     packets: 'tlshello',
        //     length: '10-20',
        //     interval: '10-20',
        //     maxSplit: '3-6',
        //   ),
        // ),
        // FragmentationPreset(
        //   'light',
        //   FragmentationSettings(
        //     enabled: true,
        //     packets: 'tlshello',
        //     length: '100-200',
        //     interval: '5-10',
        //     maxSplit: '2-4',
        //   ),
        // ),
        // FragmentationPreset('disabled', FragmentationSettings.disabled),

        return presets;
    }

    public static readonly IReadOnlyList<string> FingerprintCandidates = new[]
    {
        "firefox",
        "chrome",
        "safari",
        "edge"
    };

    public static async Task<FragmentationSettings> LoadAsync()
    {
        var dto = await StorageService.LoadPrefsAsync();
        return new FragmentationSettings
        {
            Enabled = dto.fragmentation_enabled ?? FragmentationSettings.Defaults.Enabled,
            Packets = dto.fragmentation_packets ?? FragmentationSettings.Defaults.Packets,
            Length = dto.fragmentation_length ?? FragmentationSettings.Defaults.Length,
            Interval = dto.fragmentation_interval ?? FragmentationSettings.Defaults.Interval,
            MaxSplit = dto.fragmentation_max_split ?? FragmentationSettings.Defaults.MaxSplit
        };
    }

    public static async Task SaveAsync(FragmentationSettings settings)
    {
        var dto = await StorageService.LoadPrefsAsync();
        dto.fragmentation_enabled = settings.Enabled;
        dto.fragmentation_packets = settings.Packets;
        dto.fragmentation_length = settings.Length;
        dto.fragmentation_interval = settings.Interval;
        dto.fragmentation_max_split = settings.MaxSplit;
        await StorageService.SavePrefsAsync(dto);
    }

    public static async Task<bool> IsAutoTuneEnabledAsync()
    {
        var dto = await StorageService.LoadPrefsAsync();
        return dto.fragmentation_auto ?? false;
    }

    public static async Task SetAutoTuneEnabledAsync(bool value)
    {
        var dto = await StorageService.LoadPrefsAsync();
        dto.fragmentation_auto = value;
        await StorageService.SavePrefsAsync(dto);
    }

    public static async Task<int> GetLastPresetIndexAsync()
    {
        var dto = await StorageService.LoadPrefsAsync();
        return dto.fragmentation_last_profile ?? 0;
    }

    public static async Task SetLastPresetIndexAsync(int index)
    {
        var dto = await StorageService.LoadPrefsAsync();
        dto.fragmentation_last_profile = index;
        await StorageService.SavePrefsAsync(dto);
    }

    public static async Task<bool> IsFingerprintAutoEnabledAsync()
    {
        var dto = await StorageService.LoadPrefsAsync();
        return dto.fingerprint_auto ?? false;
    }

    public static async Task SetFingerprintAutoEnabledAsync(bool value)
    {
        var dto = await StorageService.LoadPrefsAsync();
        dto.fingerprint_auto = value;
        await StorageService.SavePrefsAsync(dto);
    }

    public static async Task<string?> GetFingerprintOverrideAsync()
    {
        var dto = await StorageService.LoadPrefsAsync();
        return dto.fingerprint_override;
    }

    public static async Task SetFingerprintOverrideAsync(string? fingerprint)
    {
        var dto = await StorageService.LoadPrefsAsync();
        dto.fingerprint_override = string.IsNullOrWhiteSpace(fingerprint) ? null : fingerprint;
        await StorageService.SavePrefsAsync(dto);
    }

    public static async Task SetFragmentationEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            await SaveAsync(FragmentationSettings.Defaults);
            await SetAutoTuneEnabledAsync(true);
            await SetFingerprintAutoEnabledAsync(true);
        }
        else
        {
            var current = await LoadAsync();
            await SaveAsync(current.CopyWith(enabled: false));
            await SetAutoTuneEnabledAsync(false);
            await SetFingerprintAutoEnabledAsync(false);
        }
    }
}
