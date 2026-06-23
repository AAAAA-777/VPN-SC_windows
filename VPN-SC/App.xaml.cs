using System.Net;
using System.Windows;
using System.Windows.Threading;
using VpnSc.Localization;
using VpnSc.Services;

namespace VpnSc;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        LocalNotificationService.Initialize();
        base.OnStartup(e);

        if (!SingleInstanceService.TryAcquire())
        {
            Shutdown();
            return;
        }

        try
        {
            await VpnModeSwitch.CleanupOnStartupAsync();
            var lang = await LanguageService.GetSavedLanguageAsync();
            I18n.SetLanguage(lang);
            await StorageService.MigrateUnencryptedDataAsync();
        }
        catch
        {
            I18n.SetLanguage("ru");
        }

        var main = new MainWindow();
        main.Show();
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "VPN-SC", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            MessageBox.Show(ex.Message, "VPN-SC", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
