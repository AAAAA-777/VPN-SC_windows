using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VpnSc.Localization;
using VpnSc.Navigation;
using VpnSc.Services;

namespace VpnSc.ViewModels;

public partial class MainViewModel : ObservableObject
{

    private string? _connectedServerRaw;

    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _durationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _resendTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    [ObservableProperty] private AppPage _currentPage = AppPage.Loading;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _loadingText = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private bool _vpnConnected;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private string? _selectedServer;
    [ObservableProperty] private string _connectionStatusText = "";
    [ObservableProperty] private string _pickerServerName = "";
    [ObservableProperty] private bool _serverIsSmartLocation;
    [ObservableProperty] private ImageSource? _serverFlagImage;
    [ObservableProperty] private string _connectedServerLabel = "";
    [ObservableProperty] private bool _serversEmpty;
    [ObservableProperty] private string _serversNotFoundText = "";
    [ObservableProperty] private string _connectionDurationText = "00:00";
    [ObservableProperty] private string _trafficUplinkText = "";
    [ObservableProperty] private string _trafficDownlinkText = "";
    [ObservableProperty] private bool _showConnectionDetails;
    [ObservableProperty] private int _resendSecondsLeft;
    [ObservableProperty] private bool _canResendCode = true;
    [ObservableProperty] private string _resendButtonText = "";

    [ObservableProperty] private string _appName = "";
    [ObservableProperty] private string _appTagline = "";
    [ObservableProperty] private string _noInternetTitle = "";
    [ObservableProperty] private string _noInternetMessage = "";
    [ObservableProperty] private string _retryText = "";
    [ObservableProperty] private string _blockedTitle = "";
    [ObservableProperty] private string _blockedMessage = "";
    [ObservableProperty] private string _updateAppButtonText = "";
    [ObservableProperty] private string _loginTitleText = "";
    [ObservableProperty] private string _loginDescriptionText = "";
    [ObservableProperty] private string _loginSubdescriptionText = "";
    [ObservableProperty] private string _emailLabelText = "";
    [ObservableProperty] private string _getCodeText = "";
    [ObservableProperty] private string _verifyTitleText = "";
    [ObservableProperty] private string _verifySubtitleText = "";
    [ObservableProperty] private string _codeHintText = "";
    [ObservableProperty] private string _verifyBtnText = "";
    [ObservableProperty] private string _noCodeReceivedText = "";
    [ObservableProperty] private string _changeEmailText = "";
    [ObservableProperty] private string _subBlockedTitle = "";
    [ObservableProperty] private string _subBlockedMessage = "";
    [ObservableProperty] private string _subscribeButtonText = "";
    [ObservableProperty] private string _checkSubscriptionButtonText = "";
    [ObservableProperty] private string _logoutText = "";
    [ObservableProperty] private string _settingsTooltip = "";
    [ObservableProperty] private string _profileTooltip = "";
    [ObservableProperty] private string _logoutTooltip = "";
    [ObservableProperty] private string _selectServerText = "";
    [ObservableProperty] private string _connectButtonText = "";
    [ObservableProperty] private string _disconnectButtonText = "";
    [ObservableProperty] private string _trafficEncryptedText = "";
    [ObservableProperty] private string _backText = "";

    // Settings
    [ObservableProperty] private string _settingsTitleText = "";
    [ObservableProperty] private string _autostartTitleText = "";
    [ObservableProperty] private string _autostartSubText = "";
    [ObservableProperty] private bool _autostartEnabled;
    [ObservableProperty] private string _protocolTitleText = "";
    [ObservableProperty] private string _protocolSubText = "";
    [ObservableProperty] private string _protocolValueText = "";
    [ObservableProperty] private int _protocolIndex;
    [ObservableProperty] private bool _showWireGuardProtocol;
    [ObservableProperty] private bool _fragmentationEnabled;
    [ObservableProperty] private bool _showFragmentationSetting = true;
    [ObservableProperty] private string _fragmentationTitleText = "";
    [ObservableProperty] private string _fragmentationSubText = "";
    [ObservableProperty] private string _languageTitleText = "";
    [ObservableProperty] private string _languageSubText = "";
    [ObservableProperty] private int _languageIndex;
    [ObservableProperty] private string _appInfoTitleText = "";
    [ObservableProperty] private string _versionInfoText = "";
    [ObservableProperty] private string _infoProtocolText = "";
    [ObservableProperty] private string _infoAutostartText = "";
    [ObservableProperty] private bool _updateBannerVisible;
    [ObservableProperty] private string _updateBannerText = "";
    [ObservableProperty] private string _checkUpdatesText = "";

    // Profile
    [ObservableProperty] private string _profileTitleText = "";
    [ObservableProperty] private string _profileEmail = "—";
    [ObservableProperty] private string _profileVerifiedText = "";
    [ObservableProperty] private string _profileSubscriptionText = "";
    [ObservableProperty] private string _deviceManagementText = "";

    // Sessions
    [ObservableProperty] private string _sessionsTitleText = "";
    [ObservableProperty] private string _terminateSessionText = "";
    [ObservableProperty] private SessionItemViewModel? _selectedSession;

    public ObservableCollection<string> Servers { get; } = new();
    public ObservableCollection<ServerItemViewModel> ServerItems { get; } = new();
    public ObservableCollection<SessionItemViewModel> SessionItems { get; } = new();
    public ObservableCollection<string> LanguageOptions { get; } = new();
    public ObservableCollection<string> ProtocolOptions { get; } = new();

    public bool UsesWhiteChrome => true;

    private string? _accessToken;
    private string _pendingEmail = "";
    private DateTime? _connectionStartTime;
    private readonly Stack<AppPage> _navStack = new();
    private readonly Dictionary<string, string> _wgServerIdByName = new(StringComparer.Ordinal);
    private bool _loadingSettings;

    public MainViewModel()
    {
        LanguageOptions.Add("Русский");
        LanguageOptions.Add("English");
        _statsTimer.Tick += async (_, _) => await RefreshStatsAsync();
        _durationTimer.Tick += (_, _) => UpdateConnectionDuration();
        _resendTimer.Tick += (_, _) => TickResendTimer();
        RefreshTexts();
    }

    partial void OnCurrentPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(UsesWhiteChrome));
        UpdatePageTitle();
        if (value == AppPage.Verify)
            StartResendTimer();
        if (value == AppPage.Settings)
            _ = LoadSettingsAsync();
        if (value == AppPage.Profile)
            _ = LoadProfileAsync();
        if (value == AppPage.Sessions)
            _ = LoadSessionsAsync();
        if (value == AppPage.ServerSelection)
            RefreshServerItems();
    }

    partial void OnSelectedServerChanged(string? value) => UpdateServerDisplay();

    partial void OnAutostartEnabledChanged(bool value)
    {
        UpdateInfoAutostartText();
        if (!_loadingSettings)
            AutostartService.SetEnabled(value);
    }

    partial void OnLanguageIndexChanged(int value)
    {
        var lang = value == 1 ? "en" : "ru";
        I18n.SetLanguage(lang);
        RefreshTexts();
        if (!_loadingSettings)
            _ = LanguageService.SaveLanguageAsync(lang);
    }

    partial void OnProtocolIndexChanged(int value)
    {
        UpdateProtocolDisplay();
        if (!_loadingSettings && CurrentPage == AppPage.Settings)
            _ = OnProtocolChangedInSettingsAsync();
    }

    partial void OnFragmentationEnabledChanged(bool value)
    {
        if (!_loadingSettings && CurrentPage == AppPage.Settings)
            _ = FragmentationSettingsService.SetFragmentationEnabledAsync(value);
    }

    public void RefreshTexts()
    {
        AppName = I18n.T("app_name");
        AppTagline = I18n.T("app_tagline");
        LoadingText = I18n.T("loading");
        BackText = I18n.T("back");
        NoInternetTitle = I18n.T("no_internet_title");
        NoInternetMessage = I18n.T("no_internet_message");
        RetryText = I18n.T("try_again");
        BlockedTitle = I18n.T("version_blocked_title");
        BlockedMessage = I18n.T("version_blocked_message");
        UpdateAppButtonText = I18n.T("update_app_button");
        LoginTitleText = I18n.T("login_title");
        LoginDescriptionText = I18n.T("login_description");
        LoginSubdescriptionText = I18n.T("login_subdescription");
        EmailLabelText = I18n.T("email_label");
        GetCodeText = I18n.T("get_code");
        VerifyTitleText = I18n.T("verify_title");
        VerifySubtitleText = I18n.T("verification_subtitle");
        CodeHintText = I18n.T("verification_code_hint");
        VerifyBtnText = I18n.T("verify_btn");
        NoCodeReceivedText = I18n.T("no_code_received");
        ChangeEmailText = I18n.T("change_email");
        SubBlockedTitle = I18n.T("subscription_inactive_title");
        SubBlockedMessage = I18n.T("subscription_inactive_message");
        SubscribeButtonText = I18n.T("subscribe_button");
        CheckSubscriptionButtonText = I18n.T("check_subscription_button");
        LogoutText = I18n.T("logout");
        SettingsTooltip = I18n.T("settings_tooltip");
        ProfileTooltip = I18n.T("profile_tooltip");
        LogoutTooltip = I18n.T("logout_tooltip");
        SelectServerText = I18n.T("select_server");
        ServersNotFoundText = I18n.T("servers_not_found");
        ConnectButtonText = I18n.T("connect");
        DisconnectButtonText = I18n.T("disconnect");
        TrafficEncryptedText = I18n.T("traffic_encrypted");

        SettingsTitleText = I18n.T("settings_title");
        AutostartTitleText = I18n.T("autostart_title");
        AutostartSubText = I18n.T("autostart_subtitle");
        ShowWireGuardProtocol = OsHelper.IsWindows10OrGreater();
        ProtocolTitleText = I18n.T("protocol_title");
        ProtocolSubText = I18n.T("protocol_subtitle");
        FragmentationTitleText = I18n.T("fragmentation_title");
        FragmentationSubText = I18n.T("fragmentation_subtitle");
        RefreshProtocolOptions();
        UpdateProtocolDisplay();
        LanguageTitleText = I18n.T("language_title");
        LanguageSubText = I18n.T("language_subtitle");
        AppInfoTitleText = I18n.T("app_info_title");
        CheckUpdatesText = I18n.T("check_updates");
        ProfileTitleText = I18n.T("profile_title");
        ProfileVerifiedText = I18n.T("verified");
        DeviceManagementText = I18n.T("device_management");
        SessionsTitleText = I18n.T("sessions");
        TerminateSessionText = I18n.T("end_session");

        VersionInfoText = I18n.T("version_label") + ": " + AutoUpdateService.GetCurrentVersion();
        UpdateProtocolDisplay();
        UpdateResendButtonText();
        UpdateConnectionStatusText();
        UpdateServerDisplay();
        UpdateInfoAutostartText();
        UpdatePageTitle();
        if (ServerItems.Count > 0)
            RefreshServerItems();
    }

    private void UpdatePageTitle() =>
        PageTitle = CurrentPage switch
        {
            AppPage.Main => AppName,
            AppPage.ServerSelection => SelectServerText,
            AppPage.Settings => SettingsTitleText,
            AppPage.Profile => ProfileTitleText,
            AppPage.Sessions => SessionsTitleText,
            _ => AppName
        };

    private void UpdateInfoAutostartText() =>
        InfoAutostartText = I18n.T("autostart") + ": " +
            (AutostartEnabled ? I18n.T("autostart_enabled") : I18n.T("autostart_disabled"));

    public async Task BootstrapAsync()
    {
        CurrentPage = AppPage.Loading;
        await AnalyticsService.SendAnalyticsAsync();
        try
        {
            if (!await ConnectivityService.HasInternetConnectionAsync())
            {
                CurrentPage = AppPage.NoInternet;
                return;
            }
            if (await AutoUpdateService.IsVersionBlockedAsync())
            {
                CurrentPage = AppPage.Blocked;
                return;
            }
            if (!FileManagerService.CheckRequiredFiles())
                await FileManagerService.DownloadMissingFilesAsync();

            var logged = await StorageService.IsLoggedInAsync();
            var token = await StorageService.GetAccessTokenAsync();
            var user = await StorageService.GetUserDataAsync();
            if (logged && !string.IsNullOrEmpty(token) && user != null)
            {
                var session = await ApiService.CheckSessionAsync(token);
                if (ApiService.IsSuccess(session))
                {
                    _accessToken = token;
                    await EnterMainFlowAsync();
                    return;
                }
                await StorageService.ClearAllAsync();
            }
            CurrentPage = AppPage.Login;
        }
        catch (Exception ex)
        {
            if (ConnectivityService.IsNetworkError(ex))
                CurrentPage = AppPage.NoInternet;
            else
            {
                await StorageService.ClearAllAsync();
                CurrentPage = AppPage.Login;
            }
        }
    }

    [RelayCommand]
    private async Task RetryInternetAsync() => await BootstrapAsync();

    [RelayCommand]
    private async Task UpdateAppAsync()
    {
        var ok = await AutoUpdateService.StartUpdateAsync();
        if (!ok)
            MessageBox.Show(I18n.T("connection_error"), I18n.T("update_app_button"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private async Task GetCodeAsync()
    {
        var mail = Email.Trim();
        if (string.IsNullOrEmpty(mail) || !mail.Contains("@"))
        {
            MessageBox.Show(I18n.T("email_hint"), I18n.T("login_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var r = await ApiService.AuthAsync(mail);
        if (ApiService.IsSuccess(r))
        {
            _pendingEmail = mail;
            CurrentPage = AppPage.Verify;
        }
        else
        {
            var err = r?.TryGetProperty("error", out var er) == true ? er.GetString() : I18n.T("connection_error");
            MessageBox.Show(err ?? "", I18n.T("login_title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        if (Code.Trim().Length != 6)
        {
            MessageBox.Show(I18n.T("code_6"), I18n.T("verify_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var deviceId = "csharp_wpf_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var r = await ApiService.VerifyCodeAsync(_pendingEmail, Code.Trim(), deviceId, "VPN Security Connect");
            if (!ApiService.IsSuccess(r) || r!.Value.TryGetProperty("access_token", out var at) == false)
            {
                var err = r?.TryGetProperty("error", out var er) == true ? er.GetString() : I18n.T("connection_error");
                MessageBox.Show(err ?? "", I18n.T("verify_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var token = at.GetString() ?? "";
            await StorageService.SaveAccessTokenAsync(token);
            await StorageService.SetLoggedInAsync(true);
            await StorageService.SaveUserDataAsync(BuildUserElement(r.Value, _pendingEmail));
            _accessToken = token;
            await EnterMainFlowAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, I18n.T("verify_title"), MessageBoxButton.OK, MessageBoxImage.Error);
            CurrentPage = AppPage.Verify;
        }
    }

    [RelayCommand]
    private async Task ResendAsync()
    {
        if (!CanResendCode || string.IsNullOrEmpty(_pendingEmail))
            return;
        await ApiService.AuthAsync(_pendingEmail);
        StartResendTimer();
    }

    [RelayCommand]
    private void ChangeEmail()
    {
        Email = _pendingEmail;
        Code = "";
        CurrentPage = AppPage.Login;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await VpnOrchestrator.StopAsync();
        _statsTimer.Stop();
        _durationTimer.Stop();
        await StorageService.ClearAllWithLogoutAsync();
        _accessToken = null;
        VpnConnected = false;
        ShowConnectionDetails = false;
        ClearNavigation();
        CurrentPage = AppPage.Login;
    }

    [RelayCommand(CanExecute = nameof(CanConnectVpn))]
    private async Task ConnectVpnAsync()
    {
        if (string.IsNullOrEmpty(_accessToken) || IsConnecting || VpnConnected)
            return;

        var uuid = await StorageService.GetUserUuidAsync();
        if (string.IsNullOrEmpty(uuid))
            return;

        var server = VpnService.ResolveServerName(SelectedServer, Servers.ToList());
        if (string.IsNullOrWhiteSpace(server))
        {
            MessageBox.Show(I18n.T("select_server"), I18n.T("app_name"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string? wgServerId = null;
        var protocol = await StorageService.GetVpnProtocolAsync();
        if (protocol == VpnProtocol.Awg)
        {
            if (!_wgServerIdByName.TryGetValue(server, out wgServerId) || string.IsNullOrEmpty(wgServerId))
            {
                MessageBox.Show(I18n.T("select_server"), I18n.T("app_name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        IsConnecting = true;
        var connected = false;
        try
        {
            var (ok, err) = await VpnOrchestrator.StartAsync(uuid, server, _accessToken!, wgServerId);
            if (!ok)
            {
                await VpnModeSwitch.StopAllAsync();
                MessageBox.Show(err ?? I18n.T("connection_error"), I18n.T("app_name"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            connected = true;
            _connectionStartTime = DateTime.UtcNow;
            _connectedServerRaw = server;
            _statsTimer.Start();
            _durationTimer.Start();
            VpnConnected = true;
            ShowConnectionDetails = true;
            UpdateServerDisplay();
            UpdateConnectionStatusText();
            LocalNotificationService.ShowVpnConnected(server);
            await RefreshStatsAsync();
        }
        catch (Exception ex)
        {
            await VpnModeSwitch.StopAllAsync();
            MessageBox.Show(ex.Message, I18n.T("app_name"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsConnecting = false;
            if (!connected)
            {
                VpnConnected = false;
                ShowConnectionDetails = false;
                UpdateConnectionStatusText();
            }
        }
    }

    [RelayCommand]
    private async Task DisconnectVpnAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;
        var disconnectedServer = _connectedServerRaw;
        await VpnOrchestrator.StopAsync();
        _statsTimer.Stop();
        _durationTimer.Stop();
        _connectionStartTime = null;
        _connectedServerRaw = null;
        ConnectionDurationText = "00:00";
        VpnConnected = false;
        ShowConnectionDetails = false;
        TrafficUplinkText = "";
        TrafficDownlinkText = "";
        UpdateConnectionStatusText();
        LocalNotificationService.ShowVpnDisconnected(disconnectedServer);
    }

    [RelayCommand(CanExecute = nameof(CanChangeServer))]
    private void OpenServerSelection()
    {
        if (Servers.Count == 0)
            return;
        NavigateTo(AppPage.ServerSelection);
        RefreshServerItems();
    }

    private bool CanChangeServer() => !VpnConnected && !IsConnecting;

    private bool CanConnectVpn() => !VpnConnected && !IsConnecting;

    partial void OnVpnConnectedChanged(bool value)
    {
        OpenServerSelectionCommand.NotifyCanExecuteChanged();
        ConnectVpnCommand.NotifyCanExecuteChanged();
        UpdateConnectionStatusText();
        UpdateServerDisplay();
    }

    partial void OnIsConnectingChanged(bool value)
    {
        OpenServerSelectionCommand.NotifyCanExecuteChanged();
        ConnectVpnCommand.NotifyCanExecuteChanged();
        UpdateConnectionStatusText();
    }

    [RelayCommand]
    private void SelectServer(ServerItemViewModel? item)
    {
        if (item == null)
            return;
        SelectedServer = item.RawName;
        foreach (var s in ServerItems)
            s.IsSelected = s.RawName == item.RawName;
        NavigateBack();
    }

    [RelayCommand]
    private void OpenProfile()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;
        NavigateTo(AppPage.Profile);
    }

    [RelayCommand]
    private void OpenSettings() => NavigateTo(AppPage.Settings);

    [RelayCommand]
    private void OpenSessions()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;
        NavigateTo(AppPage.Sessions);
    }

    [RelayCommand]
    private async Task GoBack()
    {
        if (CurrentPage == AppPage.Settings)
        {
            await SaveSettingsAsync();
            await ReloadServersForProtocolAsync();
        }
        NavigateBack();
    }

    private void NavigateTo(AppPage page)
    {
        if (CurrentPage == page)
            return;
        if (CurrentPage is AppPage.Main or AppPage.ServerSelection or AppPage.Settings
            or AppPage.Profile or AppPage.Sessions)
            _navStack.Push(CurrentPage);
        CurrentPage = page;
    }

    private void NavigateBack()
    {
        CurrentPage = _navStack.Count > 0 ? _navStack.Pop() : AppPage.Main;
    }

    private void ClearNavigation() => _navStack.Clear();

    [RelayCommand]
    private async Task CheckUpdatesAsync()
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

    [RelayCommand]
    private void Subscribe()
    {
        try { Process.Start(new ProcessStartInfo("https://vpn-sc.com/") { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async Task CheckSubscriptionAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;
        var sub = await ApiService.CheckSubscriptionAsync(_accessToken);
        if (sub.ok && sub.hasSubscription)
            await EnterMainFlowAsync();
        else
            MessageBox.Show(I18n.T("subscription_inactive_message"), I18n.T("subscription_inactive_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task TerminateSessionAsync()
    {
        if (string.IsNullOrEmpty(_accessToken) || SelectedSession == null || string.IsNullOrEmpty(SelectedSession.Id))
            return;
        var r = await ApiService.TerminateSessionAsync(_accessToken, SelectedSession.Id);
        if (ApiService.IsSuccess(r))
            await LoadSessionsAsync();
        else
            MessageBox.Show(
                r?.TryGetProperty("error", out var er) == true ? er.GetString() : "?",
                I18n.T("sessions"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public async Task OnClosingAsync()
    {
        try { await VpnOrchestrator.StopAsync(); } catch { /* ignore */ }
    }

    private async Task EnterMainFlowAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            CurrentPage = AppPage.Login;
            return;
        }

        await VpnModeSwitch.StopAllAsync();
        VpnConnected = false;
        ShowConnectionDetails = false;
        _connectedServerRaw = null;
        _statsTimer.Stop();
        _durationTimer.Stop();

        CurrentPage = AppPage.Loading;
        var sub = await ApiService.CheckSubscriptionAsync(_accessToken);
        if (!sub.ok || !sub.hasSubscription)
        {
            CurrentPage = AppPage.SubBlocked;
            return;
        }
        await LoadServersAsync();
        await RefreshStatsAsync();
        ClearNavigation();
        CurrentPage = AppPage.Main;
        RefreshTexts();
    }

    private async Task LoadServersAsync()
    {
        Servers.Clear();
        _wgServerIdByName.Clear();

        var protocol = await StorageService.GetVpnProtocolAsync();
        if (protocol == VpnProtocol.Awg)
        {
            await LoadAwgServersAsync();
            return;
        }

        var uuid = await StorageService.GetUserUuidAsync();
        if (string.IsNullOrEmpty(uuid))
            return;

        var (ok, list, err) = await VpnService.GetServersAsync(uuid);
        if (!ok)
        {
            ServersEmpty = true;
            if (!string.IsNullOrEmpty(err))
                MessageBox.Show(I18n.T("servers_load_error") + ": " + err, I18n.T("app_name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateServerDisplay();
            return;
        }

        foreach (var s in list)
            Servers.Add(s);

        ServersEmpty = Servers.Count == 0;
        if (Servers.Count > 0)
        {
            if (string.IsNullOrEmpty(SelectedServer) ||
                !Servers.Any(s => string.Equals(s, SelectedServer, StringComparison.Ordinal)))
                SelectedServer = Servers[0];
        }
        else
            SelectedServer = null;

        RefreshServerItems();
        UpdateServerDisplay();
    }

    private async Task LoadAwgServersAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;

        var (ok, list, err) = await AwgVpnService.GetServersAsync(_accessToken);
        if (!ok)
        {
            ServersEmpty = true;
            if (!string.IsNullOrEmpty(err))
                MessageBox.Show(I18n.T("servers_load_error") + ": " + err, I18n.T("app_name"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateServerDisplay();
            return;
        }

        foreach (var server in list)
        {
            Servers.Add(server.DisplayName);
            _wgServerIdByName[server.DisplayName] = server.Id;
        }

        ServersEmpty = Servers.Count == 0;
        if (Servers.Count > 0)
        {
            if (string.IsNullOrEmpty(SelectedServer) ||
                !Servers.Any(s => string.Equals(s, SelectedServer, StringComparison.Ordinal)))
                SelectedServer = Servers[0];
        }
        else
            SelectedServer = null;

        RefreshServerItems();
        UpdateServerDisplay();
    }

    private async Task ReloadServersForProtocolAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;
        SelectedServer = null;
        await LoadServersAsync();
    }

    private void RefreshServerItems()
    {
        ServerItems.Clear();
        foreach (var s in Servers)
        {
            ServerItems.Add(new ServerItemViewModel
            {
                RawName = s,
                Title = s,
                Subtitle = FlagService.GetCountryName(s),
                FlagImage = FlagService.GetFlagImage(s),
                IsSmartLocation = FlagService.IsSmartLocation(s),
                ShowRecommended = FlagService.IsSmartLocation(s),
                RecommendedText = I18n.T("recommended"),
                IsSelected = string.Equals(s, SelectedServer, StringComparison.Ordinal)
            });
        }
    }

    private async Task LoadSettingsAsync()
    {
        _loadingSettings = true;
        try
        {
            var lang = await LanguageService.GetSavedLanguageAsync();
            LanguageIndex = lang == "en" ? 1 : 0;
            AutostartEnabled = AutostartService.IsEnabled();
            ShowWireGuardProtocol = OsHelper.IsWindows10OrGreater();
            RefreshProtocolOptions();

            var fragmentation = await FragmentationSettingsService.LoadAsync();
            if (fragmentation.Enabled)
            {
                await FragmentationSettingsService.SetAutoTuneEnabledAsync(true);
                await FragmentationSettingsService.SetFingerprintAutoEnabledAsync(true);
            }
            FragmentationEnabled = fragmentation.Enabled;

            var savedProtocol = await StorageService.GetVpnProtocolAsync();
            ProtocolIndex = ProtocolToIndex(savedProtocol);
            UpdateProtocolDisplay();
            VersionInfoText = I18n.T("version_label") + ": " + AutoUpdateService.GetCurrentVersion();
            UpdateInfoAutostartText();

            var (hasUpdate, _, latest) = await AutoUpdateService.CheckForUpdatesAsync();
            UpdateBannerVisible = hasUpdate;
            UpdateBannerText = hasUpdate ? I18n.T("update_available") + " — " + latest : "";
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void RefreshProtocolOptions()
    {
        var idx = ProtocolIndex;
        ProtocolOptions.Clear();
        ProtocolOptions.Add(VpnProtocolExtensions.AutoDisplayName);
        ProtocolOptions.Add(I18n.T("protocol_stealth"));
        if (ShowWireGuardProtocol)
            ProtocolOptions.Add(I18n.T("protocol_amneziawg"));
        if (ProtocolOptions.Count > 0)
            ProtocolIndex = Math.Min(idx, ProtocolOptions.Count - 1);
    }

    private void UpdateProtocolDisplay()
    {
        var protocol = IndexToProtocol(ProtocolIndex);
        ProtocolValueText = protocol switch
        {
            VpnProtocol.Awg => I18n.T("protocol_amneziawg"),
            VpnProtocol.Auto => VpnProtocolExtensions.AutoDisplayName,
            _ => I18n.T("protocol_stealth")
        };
        ShowFragmentationSetting = protocol is VpnProtocol.Stealth or VpnProtocol.Auto;
        InfoProtocolText = I18n.T("protocol") + ": " + ProtocolValueText;
    }

    private async Task SaveSettingsAsync()
    {
        var lang = LanguageIndex == 1 ? "en" : "ru";
        await LanguageService.SaveLanguageAsync(lang);
        AutostartService.SetEnabled(AutostartEnabled);
        await StorageService.SaveVpnProtocolAsync(IndexToProtocol(ProtocolIndex));
        await FragmentationSettingsService.SetFragmentationEnabledAsync(FragmentationEnabled);
        UpdateProtocolDisplay();
    }

    private async Task OnProtocolChangedInSettingsAsync()
    {
        await VpnModeSwitch.StopAllAsync();
        VpnConnected = false;
        ShowConnectionDetails = false;
        _connectedServerRaw = null;
        _statsTimer.Stop();
        _durationTimer.Stop();
        await StorageService.SaveVpnProtocolAsync(IndexToProtocol(ProtocolIndex));
        UpdateProtocolDisplay();
    }

    private VpnProtocol IndexToProtocol(int index)
    {
        if (index <= 0)
            return VpnProtocol.Auto;
        if (index == 1)
            return VpnProtocol.Stealth;
        return ShowWireGuardProtocol ? VpnProtocol.Awg : VpnProtocol.Stealth;
    }

    private int ProtocolToIndex(VpnProtocol protocol) => protocol switch
    {
        VpnProtocol.Stealth => 1,
        VpnProtocol.Awg when ShowWireGuardProtocol => 2,
        _ => 0
    };

    private async Task LoadProfileAsync()
    {
        ProfileEmail = "—";
        ProfileSubscriptionText = "";
        if (string.IsNullOrEmpty(_accessToken))
            return;
        try
        {
            var json = await ApiService.GetUserInfoAsync(_accessToken);
            if (json != null && ApiService.IsSuccess(json) && json.Value.TryGetProperty("user", out var user))
                FillProfileFromUser(user);
            else
            {
                var stored = await StorageService.GetUserDataAsync();
                if (stored != null)
                    FillProfileFromUser(stored.Value);
            }
        }
        catch
        {
            ProfileEmail = "—";
        }
    }

    private void FillProfileFromUser(JsonElement user)
    {
        ProfileEmail = user.TryGetProperty("mail", out var m) ? m.GetString()
            : user.TryGetProperty("email", out var e) ? e.GetString() : "—";
        ProfileEmail ??= "—";
        var days = 0;
        if (user.TryGetProperty("subscription_days", out var sd))
        {
            if (sd.ValueKind == JsonValueKind.Number && sd.TryGetInt32(out var n))
                days = n;
            else if (sd.ValueKind == JsonValueKind.String && int.TryParse(sd.GetString(), out var ns))
                days = ns;
        }
        ProfileSubscriptionText = I18n.T("subscription_days", ("days", days));
    }

    private async Task LoadSessionsAsync()
    {
        SessionItems.Clear();
        if (string.IsNullOrEmpty(_accessToken))
            return;
        var json = await ApiService.GetSessionsListAsync(_accessToken);
        if (json == null || !ApiService.IsSuccess(json) ||
            !json.Value.TryGetProperty("active_sessions", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                continue;
            var id = "";
            if (el.TryGetProperty("id", out var idP))
                id = idP.ValueKind == JsonValueKind.String ? idP.GetString() ?? "" : idP.GetRawText();
            var name = el.TryGetProperty("device_name", out var dn) ? dn.GetString() ?? "?" : "?";
            var plat = el.TryGetProperty("platform", out var pl) ? pl.GetString() ?? "" : "";
            SessionItems.Add(new SessionItemViewModel { Id = id, Display = $"{name}  ({plat})" });
        }
    }

    private async Task RefreshStatsAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;

        if (!IsConnecting)
        {
            VpnConnected = VpnOrchestrator.IsConnected;
            ShowConnectionDetails = VpnConnected;
        }

        UpdateConnectionStatusText();

        if (!VpnConnected)
        {
            TrafficUplinkText = "";
            TrafficDownlinkText = "";
            return;
        }

        var uuid = await StorageService.GetUserUuidAsync() ?? "";
        var (_, connected, stats) = await VpnOrchestrator.GetStatusAsync(uuid);
        if (stats != null &&
            stats.TryGetPropertyValue("uplink", out var up) &&
            stats.TryGetPropertyValue("downlink", out var down))
        {
            TrafficUplinkText = "↑ " + FormatBytes(up!.GetValue<long>());
            TrafficDownlinkText = "↓ " + FormatBytes(down!.GetValue<long>());
        }
    }

    private void UpdateConnectionStatusText() =>
        ConnectionStatusText = IsConnecting
            ? I18n.T("connecting")
            : VpnConnected ? I18n.T("connected") : I18n.T("disconnected");

    private void UpdateServerDisplay()
    {
        var name = SelectedServer;
        ServerIsSmartLocation = !string.IsNullOrEmpty(name) && FlagService.IsSmartLocation(name);
        ServerFlagImage = string.IsNullOrEmpty(name) ? null : FlagService.GetFlagImage(name);
        PickerServerName = string.IsNullOrEmpty(name) ? SelectServerText : FlagService.GetCountryName(name);
        ConnectedServerLabel = VpnConnected && !string.IsNullOrEmpty(_connectedServerRaw)
            ? I18n.T("server_label", ("server", _connectedServerRaw!))
            : "";
    }

    private void UpdateConnectionDuration()
    {
        if (!VpnConnected || _connectionStartTime == null)
        {
            ConnectionDurationText = "00:00";
            return;
        }
        var d = DateTime.UtcNow - _connectionStartTime.Value;
        ConnectionDurationText = $"{(int)d.TotalMinutes:D2}:{d.Seconds:D2}";
    }

    private void StartResendTimer()
    {
        ResendSecondsLeft = 60;
        CanResendCode = false;
        _resendTimer.Start();
        UpdateResendButtonText();
    }

    private void TickResendTimer()
    {
        if (ResendSecondsLeft <= 1)
        {
            _resendTimer.Stop();
            CanResendCode = true;
            ResendButtonText = I18n.T("resend");
            return;
        }
        ResendSecondsLeft--;
        UpdateResendButtonText();
    }

    private void UpdateResendButtonText() =>
        ResendButtonText = CanResendCode
            ? I18n.T("resend")
            : I18n.T("resend_timer", ("seconds", ResendSecondsLeft));

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
        return (bytes / (1024.0 * 1024)).ToString("0.#") + " MB";
    }

    private static JsonElement BuildUserElement(JsonElement api, string email)
    {
        var node = new JsonObject();
        foreach (var p in api.EnumerateObject())
        {
            if (p.Name is "success" or "error")
                continue;
            try { node[p.Name] = JsonNode.Parse(p.Value.GetRawText()); } catch { /* skip */ }
        }
        node["mail"] = email;
        return JsonSerializer.Deserialize<JsonElement>(node.ToJsonString())!;
    }
}
