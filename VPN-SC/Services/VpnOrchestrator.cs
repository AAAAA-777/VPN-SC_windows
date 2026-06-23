using System.Text.Json.Nodes;
using VpnSc.Localization;
using VpnSc.Models;

namespace VpnSc.Services;

public static class VpnOrchestrator
{
    public static VpnProtocol ActiveProtocolSetting { get; private set; } = VpnProtocol.Auto;

    public static bool IsConnected =>
        VpnSessionService.ActiveStack == VpnActiveStack.Awg
            ? AwgTunnelService.IsConnected
            : VpnService.IsVpnRunning();

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
        await VpnModeSwitch.StopAllAsync();

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
        await VpnModeSwitch.StopAllAsync();

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

            await VpnModeSwitch.StopStealthAsync();
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

    public static async Task StopAsync()
    {
        if (AwgTunnelService.NeedsDisconnect())
            await AwgVpnService.StopVpnAsync();

        await VpnService.StopVpnAsync();
        VpnSessionService.Reset();
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
}
