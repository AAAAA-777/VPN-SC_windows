using VpnSc.Helpers;

namespace VpnSc.Services;

public static class HiddenProcessService
{
    private static readonly TimeSpan TaskKillTimeout = TimeSpan.FromSeconds(30);
    private const int TaskKillProcessNotFoundExitCode = 128;

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

    public static Task<(bool ok, string? error)> StopVpnProcessesAsync() =>
        StopVpnProcessesAsync(CancellationToken.None);

    public static async Task<(bool ok, string? error)> StopVpnProcessesAsync(CancellationToken cancellationToken)
    {
        SystemProxyService.DisableSystemProxy();
        return await ForceKillXrayAsync(cancellationToken);
    }

    private static async Task<(bool ok, string? error)> ForceKillXrayAsync(CancellationToken cancellationToken)
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
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null)
                return (false, "Failed to start taskkill.");

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
                return (false, "Timed out waiting for xray stop.");
            }

            var stdout = (await p.StandardOutput.ReadToEndAsync()).Trim();
            var stderr = (await p.StandardError.ReadToEndAsync()).Trim();
            if (p.ExitCode == 0 || p.ExitCode == TaskKillProcessNotFoundExitCode)
                return (true, null);

            if (!string.IsNullOrWhiteSpace(stderr))
                return (false, stderr);
            if (!string.IsNullOrWhiteSpace(stdout))
                return (false, stdout);
            return (false, "taskkill failed (exit " + p.ExitCode + ").");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (false, "Operation canceled");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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