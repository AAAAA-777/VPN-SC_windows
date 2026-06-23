using Microsoft.Toolkit.Uwp.Notifications;
using VpnSc.Localization;

namespace VpnSc.Services;

/// <summary>Локальные уведомления Windows Toast — как Flutter <c>LocalNotificationService</c>.</summary>
public static class LocalNotificationService
{
  private const string AppUserModelId = "VPN-SC.SecurityConnect";
  private const int IdVpnConnected = 1;
  private const int IdVpnDisconnected = 2;

  private static bool _initialized;

  public static bool IsSupported => OsHelper.IsWindows10OrGreater();

  public static void Initialize()
  {
    if (_initialized || !IsSupported)
      return;

    try
    {
      _ = ToastNotificationManagerCompat.CreateToastNotifier();
      _initialized = true;
    }
    catch
    {
      _initialized = false;
    }
  }

  public static void ShowVpnConnected(string server)
  {
    if (!_initialized || string.IsNullOrWhiteSpace(server))
      return;

    Show(
      IdVpnConnected,
      I18n.T("connected_to_server", ("server", server)));
  }

  public static void ShowVpnDisconnected(string? server = null)
  {
    if (!_initialized)
      return;

    var title = !string.IsNullOrWhiteSpace(server)
      ? I18n.T("vpn_disconnected_from", ("server", server!))
      : I18n.T("vpn_disconnected");

    Show(IdVpnDisconnected, title);
  }

  private static void Show(int id, string title)
  {
    try
    {
      new ToastContentBuilder()
        .AddText(title)
        .Show(toast =>
        {
          toast.Tag = id.ToString();
          toast.Group = "VpnSc";
        });
    }
    catch
    {
      /* ignore */
    }
  }
}
