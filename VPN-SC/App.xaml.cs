using System.Net;
using System.Windows;
using System.Windows.Threading;
using VpnSc.Localization;
using VpnSc.Services;

namespace VpnSc;

public partial class App : Application
{
    private static readonly TimeSpan StartupCleanupTimeout = TimeSpan.FromSeconds(30);
    private static Task _startupPreparationTask = Task.CompletedTask;

    public static Task WaitForStartupPreparationAsync() => _startupPreparationTask;

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
            var lang = await LanguageService.GetSavedLanguageAsync();
            I18n.SetLanguage(lang);
        }
        catch
        {
            I18n.SetLanguage("ru");
        }

        _startupPreparationTask = RunStartupPreparationAsync();

        var main = new MainWindow();
        main.Show();
    }

    private static async Task RunStartupPreparationAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(StartupCleanupTimeout);
            try
            {
                await VpnModeSwitch.CleanupOnStartupAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                /* ignore startup cleanup timeout */
            }

            await StorageService.MigrateUnencryptedDataAsync();
        }
        catch
        {
            /* ignore startup preparation failures */
        }
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
