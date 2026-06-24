using VpnSc.Localization;
using VpnSc.Models;

namespace VpnSc.Services;

public static class AwgVpnService
{
    private const int InternetProbeAttempts = 8;
    private static readonly TimeSpan InternetProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TunnelWarmupDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan InternetProbeDelay = TimeSpan.FromSeconds(2);

    public static async Task<(bool ok, List<WireguardServer> servers, string? error)> GetServersAsync(
        string accessToken)
    {
        if (!OsHelper.IsWindows10OrGreater())
            return (false, new List<WireguardServer>(), I18n.T("wireguard_win10_required"));

        try
        {
            var servers = await WireguardApiService.FetchServersAsync(accessToken);
            return (true, servers, null);
        }
        catch (WireguardApiException ex)
        {
            return (false, new List<WireguardServer>(), ex.Message);
        }
        catch (Exception)
        {
            return (false, new List<WireguardServer>(), I18n.T("servers_load_error"));
        }
    }

    public static async Task<(bool ok, string? error)> StartVpnAsync(string accessToken, string serverId)
    {
        if (!OsHelper.IsWindows10OrGreater())
            return (false, I18n.T("wireguard_win10_required"));

        try
        {
            await VpnModeSwitch.StopStealthAsync();

            if (AwgTunnelService.NeedsDisconnect())
            {
                await AwgTunnelService.DisconnectAsync();
                await Task.Delay(500);
            }

            var payload = await WireguardApiService.FetchConnectConfigAsync(accessToken, serverId);
            var confIni = await ConfigPayloadToIniAsync(payload);
            var connect = await AwgTunnelService.ConnectAsync(confIni);
            if (!connect.ok)
                return (false, LocalizeAwgError(connect.error));

            if (!IsTunnelUp())
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                if (!IsTunnelUp())
                {
                    await AwgTunnelService.DisconnectAsync();
                    AwgTunnelService.DeleteWrittenConfigIfExists();
                    return (false, LocalizeAwgError(connect.error) ?? I18n.T("awg_tunnel_not_running"));
                }
            }

            if (!await WaitForInternetAsync())
            {
                await AwgTunnelService.DisconnectAsync();
                AwgTunnelService.DeleteWrittenConfigIfExists();
                return (false, I18n.T("no_internet"));
            }

            AwgTunnelService.ScheduleConfigDeletion();
            return (true, null);
        }
        catch (WireguardApiException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            await AwgTunnelService.DisconnectAsync();
            AwgTunnelService.DeleteWrittenConfigIfExists();
            return (false, LocalizeAwgError(ex.Message) ?? ex.Message);
        }
    }

    public static Task<(bool ok, string? error)> StopVpnAsync() =>
        StopVpnAsync(CancellationToken.None);

    public static async Task<(bool ok, string? error)> StopVpnAsync(CancellationToken cancellationToken) =>
        await AwgTunnelService.DisconnectAsync(cancellationToken);

    public static (bool ok, bool connected) GetVpnStatus() =>
        (true, IsTunnelUp());

    public static async Task<string> ConfigPayloadToIniAsync(string payload)
    {
        var trimmed = payload.Trim();
        if (trimmed.StartsWith("[Interface]", StringComparison.Ordinal))
            return trimmed;

        var vpnUri = trimmed.StartsWith("vpn://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : "vpn://" + trimmed;
        var imported = await AwgConfigService.ImportVpnUriAsync(vpnUri);
        return imported.ConfIni;
    }

    private static bool IsTunnelUp() =>
        AwgTunnelService.IsConnected || AwgTunnelService.IsTunnelServiceRunning();

    private static async Task<bool> WaitForInternetAsync()
    {
        using var cts = new CancellationTokenSource(InternetProbeTimeout);
        try
        {
            await Task.Delay(TunnelWarmupDelay, cts.Token);
            for (var attempt = 0; attempt < InternetProbeAttempts && !cts.IsCancellationRequested; attempt++)
            {
                if (await VpnTunnelProbe.TestInternetAsync(cts.Token))
                    return true;
                if (attempt < InternetProbeAttempts - 1)
                    await Task.Delay(InternetProbeDelay, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return false;
    }

    internal static string? LocalizeAwgError(string? error)
    {
        if (error is not { Length: > 0 } message)
            return null;

        return message switch
        {
            "Administrator privileges required (UAC declined)" => I18n.T("awg_uac_required"),
            "Timed out waiting for elevated tunnel install" => I18n.T("awg_tunnel_timeout"),
            "awg_tunnel_service.exe not found next to vpn-sc.exe" => I18n.T("awg_helper_missing"),
            _ when message.StartsWith("Cannot write config file:", StringComparison.OrdinalIgnoreCase)
                => I18n.T("awg_config_write_failed", ("detail", message)),
            _ when message.StartsWith("awg_tunnel_service.exe failed", StringComparison.OrdinalIgnoreCase)
                => I18n.T("awg_helper_failed", ("detail", message)),
            _ => message
        };
    }
}
