using VpnSc.Helpers;

namespace VpnSc.Services;

public static class HiddenProcessService
{
    public static bool StartHiddenProcess(string executable, params string[] arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = BuildArgs(arguments),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? ""
            };
            using var proc = Process.Start(psi);
            return proc != null;
        }
        catch { return false; }
    }

    public static async Task StopVpnProcessesAsync()
    {
        SystemProxyService.DisableSystemProxy();
        await ForceKillXrayAsync();
    }

    private static async Task ForceKillXrayAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/f /im xray.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var p = Process.Start(psi);
            if (p != null) await ProcessCompat.WaitForExitAsync(p);
        }
        catch { }
    }

    public static async Task<bool> CheckVpnProcessesAsync(string xrayPath)
    {
        Process p = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = xrayPath,
                Arguments = "api stats --server=127.0.0.1:10085 --name inbound>>>socks-inbound>>>traffic>>>uplink",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            p = Process.Start(psi);
            if (p == null) return false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var readOut = Task.Run(() => p.StandardOutput.ReadToEnd(), cts.Token);
            try { await ProcessCompat.WaitForExitAsync(p, cts.Token); }
            catch (OperationCanceledException) { ProcessCompat.Kill(p); return false; }
            await readOut;
            return p.ExitCode == 0;
        }
        catch { return false; }
        finally { p?.Dispose(); }
    }

    private static string BuildArgs(string[] args)
    {
        return string.Join(" ", args.Select(a => a.IndexOf(' ') >= 0 ? "\"" + a + "\"" : a));
    }
}