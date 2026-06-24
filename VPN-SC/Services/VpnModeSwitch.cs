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
    public static Task CleanupOnStartupAsync() =>
        CleanupOnStartupAsync(CancellationToken.None);

    public static async Task CleanupOnStartupAsync(CancellationToken cancellationToken)
    {
        SystemProxyService.DisableSystemProxy();
        cancellationToken.ThrowIfCancellationRequested();
        await HiddenProcessService.StopVpnProcessesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (AwgTunnelService.NeedsDisconnect())
            await AwgTunnelService.DisconnectAsync(cancellationToken);
        VpnSessionService.Reset();
    }

    public static async Task StopAllAsync()
    {
        await StopAwgAsync();
        await StopStealthAsync();
        VpnSessionService.Reset();
    }
}
