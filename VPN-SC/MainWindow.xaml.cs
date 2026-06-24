using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VpnSc.Navigation;
using VpnSc.Services;
using VpnSc.ViewModels;
using Wpf.Ui.Controls;

namespace VpnSc;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }
    private bool _allowClose;
    private bool _closeInProgress;

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        SingleInstanceService.RegisterMainWindow(this);
        ApplyWindowIcon();
        AppTitleBar.Loaded += (_, _) => ConfigureTitleBar();

        if (!OsHelper.IsWindows10OrGreater())
            WindowBackdropType = WindowBackdropType.None;

        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");

        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        SourceInitialized += (_, _) => WindowLayoutService.ApplyTo(this);
        Loaded += MainWindow_OnLoaded;
        ContentRendered += (_, _) => WindowLayoutService.ApplyTo(this);
        Closing += MainWindow_OnClosing;
        ApplyPageTheme();
    }

    private void ConfigureTitleBar()
    {
        AppTitleBar.Icon = null;
        AppTitleBar.Padding = new Thickness(12, 0, 0, 0);
        AppTitleBar.Margin = new Thickness(0);

        if (AppTitleBar.Template.FindName("PART_Icon", AppTitleBar) is FrameworkElement icon)
            icon.Visibility = Visibility.Collapsed;

        if (AppTitleBar.Template.FindName("PART_MainGrid", AppTitleBar) is Grid mainGrid
            && mainGrid.ColumnDefinitions.Count > 0)
        {
            mainGrid.ColumnDefinitions[0].Width = new GridLength(0);
            mainGrid.Margin = new Thickness(12, 0, 0, 0);
            mainGrid.VerticalAlignment = VerticalAlignment.Stretch;
        }

        if (AppTitleBar.Template.FindName("PART_Header", AppTitleBar) is FrameworkElement headerHost)
        {
            headerHost.Margin = new Thickness(12, 0, 0, 0);
            headerHost.VerticalAlignment = VerticalAlignment.Center;
        }

        if (AppTitleBar.Header is FrameworkElement header)
        {
            header.Margin = new Thickness(0);
            header.VerticalAlignment = VerticalAlignment.Center;
        }

        foreach (var partName in new[] { "PART_CloseButton", "PART_MinimizeButton", "PART_MaximizeButton" })
        {
            if (AppTitleBar.Template.FindName(partName, AppTitleBar) is FrameworkElement button)
            {
                button.Margin = new Thickness(0);
                button.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }
    }

    private void ApplyWindowIcon()
    {
        try
        {
            Icon = BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/app_icon.ico", UriKind.Absolute));
        }
        catch
        {
            /* ignore */
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentPage))
            ApplyPageTheme();
    }

    private void ApplyPageTheme()
    {
        Background = (Brush)FindResource("VpnPageBackgroundBrush");
        AppTitleBar.ButtonsForeground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        AppTitleBar.ShowMinimize = true;
        AppTitleBar.ShowMaximize = true;
        AppTitleBar.CanMaximize = false;
        AppTitleBar.ShowClose = true;
        ConfigureTitleBar();
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowLayoutService.ApplyTo(this);
        try
        {
            await App.WaitForStartupPreparationAsync();
        }
        catch
        {
            /* ignore startup prep errors */
        }
        await ViewModel.BootstrapAsync();
    }

    private async void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        e.Cancel = true;
        if (_closeInProgress)
            return;

        _closeInProgress = true;
        IsEnabled = false;
        try
        {
            WindowLayoutService.SavePosition(this);
            await ViewModel.OnClosingAsync();
        }
        finally
        {
            _allowClose = true;
            _closeInProgress = false;
            Dispatcher.BeginInvoke(new Action(Close));
        }
    }
}
