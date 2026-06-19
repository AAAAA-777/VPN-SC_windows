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
        if (AwgTunnelService.IsConnected)
            await AwgTunnelService.DisconnectAsync();
    }

    public static async Task StopAllAsync()
    {
        await StopAwgAsync();
        await StopStealthAsync();
        VpnSessionService.Reset();
    }
}
