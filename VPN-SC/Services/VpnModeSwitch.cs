namespace VpnSc.Services;

public static class VpnModeSwitch
{
    public static async Task StopStealthAsync()
    {
        _ = await StopStealthAsync(CancellationToken.None);
    }

    public static async Task<(bool ok, string? error)> StopStealthAsync(CancellationToken cancellationToken)
    {
        var stopResult = await HiddenProcessService.StopVpnProcessesAsync(cancellationToken);
        SystemProxyService.DisableSystemProxy();
        return stopResult;
    }

    public static async Task StopAwgAsync()
    {
        _ = await StopAwgAsync(CancellationToken.None);
    }

    public static async Task<(bool ok, string? error)> StopAwgAsync(CancellationToken cancellationToken)
    {
        if (!AwgTunnelService.NeedsDisconnect())
            return (true, null);
        return await AwgTunnelService.DisconnectAsync(cancellationToken);
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
        var stealthStop = await HiddenProcessService.StopVpnProcessesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var awgStop = (ok: true, error: (string?)null);
        if (AwgTunnelService.NeedsDisconnect())
            awgStop = await AwgTunnelService.DisconnectAsync(cancellationToken);

        if (stealthStop.ok && awgStop.ok)
            VpnSessionService.Reset();
    }

    public static async Task StopAllAsync()
    {
        _ = await StopAllAsync(CancellationToken.None);
    }

    public static async Task<(bool ok, string? error)> StopAllAsync(CancellationToken cancellationToken)
    {
        string? error = null;
        var awgStop = await StopAwgAsync(cancellationToken);
        if (!awgStop.ok)
            error ??= awgStop.error;

        cancellationToken.ThrowIfCancellationRequested();
        var stealthStop = await StopStealthAsync(cancellationToken);
        if (!stealthStop.ok)
            error ??= stealthStop.error;

        if (!string.IsNullOrWhiteSpace(error))
            return (false, error);

        VpnSessionService.Reset();
        return (true, null);
    }
}
