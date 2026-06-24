using VpnSc.Helpers;

namespace VpnSc.Services;

public static class HiddenProcessService
{
    private static readonly TimeSpan TaskKillTimeout = TimeSpan.FromSeconds(30);

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

    public static Task StopVpnProcessesAsync() =>
        StopVpnProcessesAsync(CancellationToken.None);

    public static async Task StopVpnProcessesAsync(CancellationToken cancellationToken)
    {
        SystemProxyService.DisableSystemProxy();
        await ForceKillXrayAsync(cancellationToken);
    }

    private static async Task ForceKillXrayAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/f /im xray.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var p = Process.Start(psi);
            if (p == null)
                return;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TaskKillTimeout);
            try
            {
                await ProcessCompat.WaitForExitAsync(p, cts.Token);
            }
            catch (OperationCanceledException)
            {
                ProcessCompat.Kill(p);
                if (cancellationToken.IsCancellationRequested)
                    throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch { }
    }

    public static async Task<bool> CheckVpnProcessesAsync(string xrayPath)
    {
        Process? p = null;
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