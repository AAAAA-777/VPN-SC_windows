using VpnSc.Localization;
using VpnSc.Models;

namespace VpnSc.Services;

public static class AwgVpnService
{
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
                return connect;

            if (!AwgTunnelService.IsConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                if (!AwgTunnelService.IsConnected)
                {
                    await AwgTunnelService.DisconnectAsync();
                    AwgTunnelService.DeleteWrittenConfigIfExists();
                    return (false, I18n.T("connection_error"));
                }
            }

            if (!await VpnTunnelProbe.TestInternetAsync())
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
        catch (Exception)
        {
            await AwgTunnelService.DisconnectAsync();
            AwgTunnelService.DeleteWrittenConfigIfExists();
            return (false, I18n.T("connection_error"));
        }
    }

    public static async Task<(bool ok, string? error)> StopVpnAsync() =>
        await AwgTunnelService.DisconnectAsync();

    public static (bool ok, bool connected) GetVpnStatus() =>
        (true, AwgTunnelService.IsConnected);

    /// <summary>
    /// Converts API payload (vpn:// URI, raw INI, or base64 vpn payload) to WireGuard INI — same as Flutter AwgVpnService.
    /// </summary>
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
}
