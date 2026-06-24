using System.Text.Json.Nodes;
using VpnSc.Localization;
using VpnSc.Models;

namespace VpnSc.Services;

public static class VpnOrchestrator
{
    public static VpnProtocol ActiveProtocolSetting { get; private set; } = VpnProtocol.Auto;

    public static bool IsConnected =>
        // Runtime truth source: treat either live tunnel as connected, regardless of saved stack.
        AwgTunnelService.NeedsDisconnect() || VpnService.IsVpnRunning();

    public static async Task<(bool ok, string? error)> StartAsync(
        string uuid,
        string serverName,
        string accessToken,
        string? wgServerId)
    {
        var protocol = await StorageService.GetVpnProtocolAsync();
        ActiveProtocolSetting = protocol;
        VpnSessionService.Reset();

        switch (protocol)
        {
            case VpnProtocol.Stealth:
                return await StartStealthAsync(uuid, serverName, abortOnAutotuneFail: false);
            case VpnProtocol.Awg:
                return await StartAwgAsync(accessToken, wgServerId, serverName);
            default:
                return await StartAutoAsync(uuid, serverName, accessToken);
        }
    }

    public static async Task<(bool ok, string? error)> StartStealthAsync(
        string uuid,
        string serverName,
        bool abortOnAutotuneFail = false)
    {
        var stopAll = await VpnModeSwitch.StopAllAsync(CancellationToken.None);
        if (!stopAll.ok)
            return (false, LocalizeStopError(stopAll.error ?? I18n.T("connection_error")));

        var result = await VpnService.StartVpnAsync(uuid, serverName, abortOnAutotuneFail);
        if (result.Success)
        {
            VpnSessionService.SetActiveStack(VpnActiveStack.Stealth);
            return (true, null);
        }

        return (false, LocalizeConnectError(result.Error));
    }

    public static async Task<(bool ok, string? error)> StartAwgAsync(
        string accessToken,
        string? wgServerId,
        string? displayServerName = null)
    {
        var stopAll = await VpnModeSwitch.StopAllAsync(CancellationToken.None);
        if (!stopAll.ok)
            return (false, LocalizeStopError(stopAll.error ?? I18n.T("connection_error")));

        if (!OsHelper.IsWindows10OrGreater())
            return (false, I18n.T("wireguard_win10_required"));

        if (wgServerId is not { Length: > 0 })
            return (false, I18n.T("select_server"));

        var awg = await AwgVpnService.StartVpnAsync(accessToken, wgServerId);
        if (!awg.ok)
            return (false, awg.error ?? I18n.T("wg_unavailable"));

        VpnSessionService.SetActiveStack(VpnActiveStack.Awg);
        return (true, null);
    }

    private static async Task<(bool ok, string? error)> StartAutoAsync(
        string uuid,
        string serverName,
        string accessToken)
    {
        var stealth = await StartStealthAsync(uuid, serverName, abortOnAutotuneFail: true);
        if (stealth.ok)
            return stealth;

        if (!OsHelper.IsWindows10OrGreater())
            return stealth;

        return await TryWgFallbackAsync(accessToken, stealth.error);
    }

    private static async Task<(bool ok, string? error)> TryWgFallbackAsync(
        string accessToken,
        string? stealthError)
    {
        try
        {
            var servers = await WireguardApiService.FetchServersAsync(accessToken);
            var picked = WireguardServer.PickFallbackServer(servers);
            if (picked == null)
                return (false, stealthError ?? I18n.T("wg_unavailable"));

            var stopStealth = await VpnModeSwitch.StopStealthAsync(CancellationToken.None);
            if (!stopStealth.ok)
                return (false, LocalizeStopError(stopStealth.error ?? stealthError ?? I18n.T("connection_error")));
            var awg = await AwgVpnService.StartVpnAsync(accessToken, picked.Id);
            if (!awg.ok)
                return (false, awg.error ?? stealthError ?? I18n.T("auto_fallback_failed"));

            VpnSessionService.SetActiveStack(VpnActiveStack.Awg);
            return (true, null);
        }
        catch (WireguardApiException ex)
        {
            return (false, stealthError ?? ex.Message);
        }
        catch (Exception)
        {
            return (false, stealthError ?? I18n.T("wg_unavailable"));
        }
    }

    public static Task<(bool ok, string? error)> StopAsync() => StopAsync(CancellationToken.None);

    public static async Task<(bool ok, string? error)> StopAsync(CancellationToken cancellationToken)
    {
        string? error = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AwgTunnelService.NeedsDisconnect())
            {
                var awgStop = await AwgVpnService.StopVpnAsync(cancellationToken);
                if (!awgStop.ok)
                    error ??= awgStop.error;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stealthStop = await VpnService.StopVpnAsync(cancellationToken);
            if (!stealthStop.ok)
                error ??= stealthStop.error;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error ??= "Operation canceled";
        }
        catch (Exception ex)
        {
            error ??= ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(error))
            return (false, LocalizeStopError(error));

        VpnSessionService.Reset();
        return (true, null);
    }

    public static async Task<(bool ok, bool connected, JsonObject? stats)> GetStatusAsync(string uuid)
    {
        var protocol = await StorageService.GetVpnProtocolAsync();
        if (protocol == VpnProtocol.Awg ||
            (protocol == VpnProtocol.Auto && VpnSessionService.ActiveStack == VpnActiveStack.Awg))
        {
            var awg = AwgVpnService.GetVpnStatus();
            return (awg.ok, awg.connected, null);
        }

        return await VpnService.GetVpnStatusAsync(uuid);
    }

    public static bool UsesStealthServerList(VpnProtocol protocol) =>
        protocol is VpnProtocol.Stealth or VpnProtocol.Auto;

    private static string? LocalizeConnectError(string? error)
    {
        if (string.IsNullOrEmpty(error))
            return I18n.T("connection_error");
        return error switch
        {
            VpnAutoTunerService.AutotuneFailedReason => I18n.T("connection_error_autotune"),
            "select_server" => I18n.T("select_server"),
            "wg_unavailable" => I18n.T("wg_unavailable"),
            "auto_fallback_failed" => I18n.T("auto_fallback_failed"),
            _ => error
        };
    }

    private static string LocalizeStopError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return I18n.T("connection_error");

        return error switch
        {
            "Operation canceled" => I18n.T("connection_error"),
            "Timed out waiting for elevated tunnel install" => I18n.T("awg_tunnel_timeout"),
            "Timed out waiting for elevated tunnel stop" => I18n.T("awg_tunnel_timeout"),
            "awg_tunnel_service.exe not found next to vpn-sc.exe" => I18n.T("awg_helper_missing"),
            _ when error.StartsWith("awg_tunnel_service.exe failed", StringComparison.OrdinalIgnoreCase)
                => I18n.T("awg_helper_failed", ("detail", error)),
            _ => error
        };
    }
}
