using System.Windows;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class WindowLayoutService
{
    private const double Margin = 20;

    public static (double width, double height) GetWindowSize()
    {
        var screenWidth = NativeWindowHelper.GetPrimaryScreenWidthPhysical();
        return GetWindowSize(screenWidth);
    }

    /// <summary>Размер окна по ширине экрана — как во Flutter <c>D:\vpn-sc\WindowPositionService._getWindowSize</c>.</summary>
    public static (double width, double height) GetWindowSize(double screenWidthPhysical)
    {
        if (screenWidthPhysical >= 3840)
            return (450, 759);
        return (450, 720);
    }

    public static void ApplyTo(Window window)
    {
        var screenWidthPhysical = NativeWindowHelper.GetPrimaryScreenWidthPhysical();
        var (width, height) = GetWindowSize(screenWidthPhysical);

        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;

        // Фиксируем размер как во Flutter (логические DIP, без повторного physical-scale).
        window.MinWidth = width;
        window.MaxWidth = width;
        window.MinHeight = height;
        window.MaxHeight = height;
        window.Width = width;
        window.Height = height;

        var saved = StorageService.GetWindowPosition();
        if (saved is { } pos && IsOnScreen(pos.left, pos.top, width, height))
        {
            window.Left = pos.left;
            window.Top = pos.top;
            return;
        }

        var defaultPos = GetDefaultPosition(width, height);
        window.Left = defaultPos.X;
        window.Top = defaultPos.Y;
    }

    public static Point GetDefaultPosition(double windowWidth, double windowHeight)
    {
        // Как Flutter: x = screenWidth - windowWidth - 20, y = 20 (в DIP первичного монитора).
        var x = SystemParameters.PrimaryScreenWidth - windowWidth - Margin;
        var y = Margin;
        return new Point(x, y);
    }

    public static void SavePosition(Window window) =>
        StorageService.SaveWindowPosition(window.Left, window.Top);

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var centerX = left + width / 2;
        var centerY = top + height / 2;
        return centerX >= SystemParameters.VirtualScreenLeft
               && centerX <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
               && centerY >= SystemParameters.VirtualScreenTop
               && centerY <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
    }
}
