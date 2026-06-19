using System.Windows;
using VpnSc.Localization;
using VpnSc.Services;

namespace VpnSc.Windows;

public partial class SettingsWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
        TxtTitle.Text = I18n.T("settings_title");
        TxtAutostartTitle.Text = I18n.T("autostart_title");
        TxtAutostartSub.Text = I18n.T("autostart_subtitle");
        TxtProtocolTitle.Text = I18n.T("protocol_title");
        TxtProtocolSub.Text = I18n.T("protocol_subtitle");
        TxtProtocolValue.Text = I18n.T("protocol_stealth");
        TxtLangTitle.Text = I18n.T("language_title");
        TxtLangSub.Text = I18n.T("language_subtitle");
        LangCombo.Items.Add(I18n.T("language_russian"));
        LangCombo.Items.Add(I18n.T("language_english"));
        TxtUpdateAvailable.Text = I18n.T("update_available");
        BtnUpdate.Content = I18n.T("check_updates");
        TxtInfoTitle.Text = I18n.T("app_info_title");
        Loaded += SettingsWindow_OnLoaded;
    }

    private async void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        var lang = await LanguageService.GetSavedLanguageAsync();
        LangCombo.SelectedIndex = lang == "en" ? 1 : 0;
        ChkAutostart.IsChecked = AutostartService.IsEnabled();
        TxtVersion.Text = I18n.T("version_label") + ": " + AutoUpdateService.GetCurrentVersion();
        TxtInfoProtocol.Text = I18n.T("protocol") + ": " + I18n.T("protocol_stealth");
        UpdateAutostartInfo();

        var (hasUpdate, _, latest) = await AutoUpdateService.CheckForUpdatesAsync();
        if (hasUpdate)
        {
            UpdateBanner.Visibility = Visibility.Visible;
            TxtUpdateAvailable.Text = I18n.T("update_available") + " — " + latest;
        }
    }

    private void UpdateAutostartInfo() =>
        TxtInfoAutostart.Text = I18n.T("autostart") + ": " +
            (ChkAutostart.IsChecked == true ? I18n.T("autostart_enabled") : I18n.T("autostart_disabled"));

    private async void BtnUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        var (has, url, latest) = await AutoUpdateService.CheckForUpdatesAsync();
        if (!has)
        {
            MessageBox.Show(AutoUpdateService.GetCurrentVersion() + " — OK",
                I18n.T("check_updates"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(latest + "\n" + url, I18n.T("update_available"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        await AutoUpdateService.StartUpdateAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        var en = LangCombo.SelectedIndex == 1;
        LanguageService.SaveLanguageAsync(en ? "en" : "ru").GetAwaiter().GetResult();
        I18n.SetLanguage(en ? "en" : "ru");
        AutostartService.SetEnabled(ChkAutostart.IsChecked == true);
        base.OnClosed(e);
    }
}
