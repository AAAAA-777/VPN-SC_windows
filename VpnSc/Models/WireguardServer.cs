namespace VpnSc.Models;

public sealed class WireguardServer
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public bool AutoConnect { get; init; }

    public static WireguardServer? PickFallbackServer(IReadOnlyList<WireguardServer> servers)
    {
        if (servers.Count == 0)
            return null;
        foreach (var server in servers)
        {
            if (server.AutoConnect)
                return server;
        }
        return servers[0];
    }
}
