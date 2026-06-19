using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using VpnSc.Localization;
using VpnSc.Services;

namespace VpnSc.Windows;

public partial class ProfileWindow
{
    private readonly string _token;

    public ProfileWindow(string token)
    {
        _token = token;
        InitializeComponent();
        TxtTitle.Text = I18n.T("profile_title");
        TxtEmailLabel.Text = I18n.T("email_label");
        TxtVerifiedLabel.Text = I18n.T("verified");
        BtnDevices.Content = I18n.T("device_management");
        Loaded += (_, _) => _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var json = await ApiService.GetUserInfoAsync(_token);
            if (json == null || !ApiService.IsSuccess(json))
            {
                var stored = await StorageService.GetUserDataAsync();
                if (stored != null)
                    FillFromUser(stored.Value);
                return;
            }
            if (json.Value.TryGetProperty("user", out var user))
                FillFromUser(user);
        }
        catch
        {
            TxtEmail.Text = "—";
        }
    }

    private void FillFromUser(JsonElement user)
    {
        var email = user.TryGetProperty("mail", out var m) ? m.GetString()
            : user.TryGetProperty("email", out var e) ? e.GetString() : "—";
        TxtEmail.Text = email ?? "—";
        var days = 0;
        if (user.TryGetProperty("subscription_days", out var sd))
        {
            if (sd.ValueKind == JsonValueKind.Number && sd.TryGetInt32(out var n))
                days = n;
            else if (sd.ValueKind == JsonValueKind.String && int.TryParse(sd.GetString(), out var ns))
                days = ns;
        }
        TxtSubscription.Text = I18n.T("subscription_days", ("days", days));
    }

    private void BtnDevices_OnClick(object sender, RoutedEventArgs e)
    {
        var w = new SessionsWindow(_token) { Owner = this };
        w.ShowDialog();
    }
}
