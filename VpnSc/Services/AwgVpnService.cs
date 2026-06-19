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
        catch (Exception ex)
        {
            return (false, new List<WireguardServer>(), ex.Message);
        }
    }

    public static async Task<(bool ok, string? error)> StartVpnAsync(string accessToken, string serverId)
    {
        if (!OsHelper.IsWindows10OrGreater())
            return (false, I18n.T("wireguard_win10_required"));

        try
        {
            await VpnModeSwitch.StopStealthAsync();
            var payload = await WireguardApiService.FetchConnectConfigAsync(accessToken, serverId);
            var imported = await AwgConfigService.ImportVpnUriAsync(payload.Trim());
            return await AwgTunnelService.ConnectAsync(imported.ConfIni);
        }
        catch (WireguardApiException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool ok, string? error)> StopVpnAsync() =>
        await AwgTunnelService.DisconnectAsync();

    public static (bool ok, bool connected) GetVpnStatus() =>
        (true, AwgTunnelService.IsConnected);
}
