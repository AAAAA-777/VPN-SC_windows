using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using VpnSc.Localization;
using VpnSc.Services;

namespace VpnSc.Windows;

public partial class SessionsWindow
{
    private readonly string _token;

    public SessionsWindow(string token)
    {
        _token = token;
        InitializeComponent();
        TxtTitle.Text = I18n.T("sessions");
        BtnTerminate.Content = I18n.T("end_session");
        BtnClose.Content = I18n.T("close");
        Loaded += (_, _) => _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var json = await ApiService.GetSessionsListAsync(_token);
        var list = new List<SessionRow>();
        if (json != null && ApiService.IsSuccess(json) &&
            json.Value.TryGetProperty("active_sessions", out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                var id = "";
                if (el.TryGetProperty("id", out var idP))
                    id = idP.ValueKind == JsonValueKind.String ? idP.GetString() ?? "" : idP.GetRawText();
                var name = el.TryGetProperty("device_name", out var dn) ? dn.GetString() ?? "?" : "?";
                var plat = el.TryGetProperty("platform", out var pl) ? pl.GetString() ?? "" : "";
                list.Add(new SessionRow(id, $"{name}  ({plat})"));
            }
        }

        SessionsList.ItemsSource = list;
    }

    private async void BtnTerminate_OnClick(object sender, RoutedEventArgs e)
    {
        if (SessionsList.SelectedItem is not SessionRow row || string.IsNullOrEmpty(row.Id))
            return;
        var r = await ApiService.TerminateSessionAsync(_token, row.Id);
        if (ApiService.IsSuccess(r))
            await LoadAsync();
        else
            MessageBox.Show(
                r?.TryGetProperty("error", out var er) == true ? er.GetString() : "?",
                I18n.T("sessions"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void BtnClose_OnClick(object sender, RoutedEventArgs e) => Close();

    private sealed record SessionRow(string Id, string Display);
}


