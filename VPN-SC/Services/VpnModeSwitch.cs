namespace VpnSc.Services;

public static class VpnModeSwitch
{
    public static async Task StopStealthAsync()
    {
        await HiddenProcessService.StopVpnProcessesAsync();
        SystemProxyService.DisableSystemProxy();
    }

    public static async Task StopAwgAsync()
    {
        if (AwgTunnelService.NeedsDisconnect())
            await AwgTunnelService.DisconnectAsync();
    }

    /// <summary>
    /// Clears stale VPN state after crash or force-kill (proxy, xray, AWG tunnel).
    /// </summary>
    public static async Task CleanupOnStartupAsync()
    {
        SystemProxyService.DisableSystemProxy();
        await HiddenProcessService.StopVpnProcessesAsync();
        if (AwgTunnelService.NeedsDisconnect())
            await AwgTunnelService.DisconnectAsync();
        VpnSessionService.Reset();
    }

    public static async Task StopAllAsync()
    {
        await StopAwgAsync();
        await StopStealthAsync();
        VpnSessionService.Reset();
    }
}
