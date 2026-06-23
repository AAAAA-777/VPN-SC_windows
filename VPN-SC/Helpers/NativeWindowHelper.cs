using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace VpnSc.Helpers;

internal static class NativeWindowHelper
{
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    public static uint GetWindowDpi(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
                return GetDpiForWindow(hwnd);
        }
        catch
        {
            /* ignore */
        }

        return (uint)Graphics.FromHwnd(IntPtr.Zero).DpiX;
    }

    public static int ScaleToPhysical(double logical, uint dpi) =>
        (int)Math.Round(logical * dpi / 96.0);

    public static double PhysicalToLogical(int physical, uint dpi) =>
        physical * 96.0 / dpi;

    public static void SetBoundsPhysical(Window window, int x, int y, int width, int height)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SwpNoZOrder | SwpNoActivate);
    }

    public static Screen GetPrimaryScreen() =>
        Screen.PrimaryScreen ?? Screen.AllScreens[0];

    public static double GetPrimaryScreenWidthPhysical() =>
        GetPrimaryScreen().Bounds.Width;
}
