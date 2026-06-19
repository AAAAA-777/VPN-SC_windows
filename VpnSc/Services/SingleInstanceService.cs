using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;

namespace VpnSc.Services;

public static class SingleInstanceService
{
    private const string MutexName = @"Local\VPN_SC_SingleInstance_Mutex";
    private const string WindowTitle = "VPN Security Connect";

    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
            return true;

        _mutex.Dispose();
        _mutex = null;
        TryActivateExistingWindow();
        return false;
    }

    public static void RegisterMainWindow(System.Windows.Window window)
    {
        if (window.IsLoaded)
            StoreHandle(window);
        else
            window.Loaded += (_, _) => StoreHandle(window);
    }

    private static void StoreHandle(System.Windows.Window window)
    {
        _mainWindowHandle = new WindowInteropHelper(window).Handle;
    }

    private static IntPtr _mainWindowHandle;

    private static void TryActivateExistingWindow()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var hwnd = _mainWindowHandle != IntPtr.Zero
                ? _mainWindowHandle
                : FindWindow(null, WindowTitle);

            if (hwnd != IntPtr.Zero)
            {
                ActivateWindow(hwnd);
                return;
            }

            Thread.Sleep(50);
        }
    }

    private static void ActivateWindow(IntPtr hwnd)
    {
        if (IsIconic(hwnd))
            ShowWindow(hwnd, SwRestore);
        else
            ShowWindow(hwnd, SwShow);

        var foreground = GetForegroundWindow();
        var foregroundThread = foreground != IntPtr.Zero
            ? GetWindowThreadProcessId(foreground, out _)
            : 0u;
        var targetThread = GetWindowThreadProcessId(hwnd, out _);
        var currentThread = GetCurrentThreadId();

        if (foregroundThread != 0 && foregroundThread != targetThread)
            AttachThreadInput(currentThread, foregroundThread, true);
        if (targetThread != currentThread)
            AttachThreadInput(currentThread, targetThread, true);

        SetForegroundWindow(hwnd);
        BringWindowToTop(hwnd);

        if (foregroundThread != 0 && foregroundThread != targetThread)
            AttachThreadInput(currentThread, foregroundThread, false);
        if (targetThread != currentThread)
            AttachThreadInput(currentThread, targetThread, false);
    }

    private const int SwRestore = 9;
    private const int SwShow = 5;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);
}
