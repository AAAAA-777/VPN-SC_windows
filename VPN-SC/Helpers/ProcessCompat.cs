using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VpnSc.Helpers;

internal static class ProcessCompat
{
    public static Task WaitForExitAsync(Process process, CancellationToken cancellationToken = default)
    {
        if (process.HasExited)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>();
        process.EnableRaisingEvents = true;
        process.Exited += (_, __) => tcs.TrySetResult(true);

        if (process.HasExited)
            return Task.CompletedTask;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch { }
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        return tcs.Task;
    }

    public static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch { }
    }
}
