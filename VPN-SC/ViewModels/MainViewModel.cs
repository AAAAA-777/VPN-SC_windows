using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
    private int _tunnelHealthFailures;
    private int _statsRefreshInProgress;

    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _durationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _resendTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    [ObservableProperty] private AppPage _currentPage = AppPage.Loading;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _loadingText = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _authErrorText = "";
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
    [ObservableProperty] private string _supportLinkText = "";
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
    [ObservableProperty] private string _infoWindowsText = "";
    [ObservableProperty] private bool _updateBannerVisible;
    [ObservableProperty] private string _updateBannerText = "";
    [ObservableProperty] private string _mainUpdateLinkText = "";

    // Profile
    [ObservableProperty] private string _profileTitleText = "";
    [ObservableProperty] private string _profileEmail = "—";
    [ObservableProperty] private string _profileSubscriptionText = "";
    [ObservableProperty] private bool _profileSubscriptionActive;
    [ObservableProperty] private string _profileSubscriptionBadgeText = "";
    [ObservableProperty] private string _profileEmailLabelText = "";
    [ObservableProperty] private string _profileSubscriptionLabelText = "";
    [ObservableProperty] private string _deviceManagementText = "";
    [ObservableProperty] private string _deviceManagementSubText = "";
    [ObservableProperty] private string _profileIdText = "";
    [ObservableProperty] private bool _isProfileLoading;

    // Sessions
    [ObservableProperty] private string _sessionsTitleText = "";
    [ObservableProperty] private bool _sessionsEmpty;
    [ObservableProperty] private string _sessionsEmptyTitle = "";
    [ObservableProperty] private string _sessionsEmptyMessage = "";

    public ObservableCollection<string> Servers { get; } = new();
    public ObservableCollection<ServerItemViewModel> ServerItems { get; } = new();
    public ObservableCollection<SessionItemViewModel> SessionItems { get; } = new();
    public ObservableCollection<string> LanguageOptions { get; } = new();
    public ObservableCollection<string> ProtocolOptions { get; } = new();

    private string? _accessToken;
    private string _pendingEmail = "";
    private DateTime? _connectionStartTime;
    private readonly Stack<AppPage> _navStack = new();
    private readonly Dictionary<string, string> _wgServerIdByName = new(StringComparer.Ordinal);
    private string? _selectedWgServerId;
    private bool _loadingSettings;
    private string? _latestAvailableVersion;
    private string? _latestDownloadUrl;

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

    partial void OnSelectedServerChanged(string? value)
    {
        if (value != null && _wgServerIdByName.TryGetValue(value, out var wgId))
            _selectedWgServerId = wgId;
        UpdateServerDisplay();
    }

    partial void OnAutostartEnabledChanged(bool value)
    {
        UpdateInfoAutostartText();
        if (!_loadingSettings)
        {
            AutostartService.SetEnabled(value);
            var actual = AutostartService.IsEnabled();
            if (actual != value)
            {
                _loadingSettings = true;
                AutostartEnabled = actual;
                _loadingSettings = false;
                UpdateInfoAutostartText();
            }
        }
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
        SupportLinkText = I18n.T("support_link");
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
        MainUpdateLinkText = I18n.T("update_main_link");
        ProfileTitleText = I18n.T("profile_title");
        ProfileEmailLabelText = I18n.T("profile_email_label");
        ProfileSubscriptionLabelText = I18n.T("profile_subscription_label");
        DeviceManagementText = I18n.T("device_management");
        DeviceManagementSubText = I18n.T("device_management_subtitle");
        SessionsTitleText = I18n.T("device_management");
        SessionsEmptyTitle = I18n.T("no_active_devices");
        SessionsEmptyMessage = I18n.T("all_devices_disconnected");
        ProfileSubscriptionBadgeText = I18n.T("subscription_inactive_title");

        VersionInfoText = I18n.T("version_label") + ": " + AutoUpdateService.GetCurrentVersion();
        UpdateProtocolDisplay();
        UpdateResendButtonText();
        UpdateConnectionStatusText();
        UpdateServerDisplay();
        UpdateInfoAutostartText();
        UpdateInfoWindowsText();
        UpdatePageTitle();
        if (ServerItems.Count > 0)
            RefreshServerItems();
    }

    private async Task RefreshUpdateStateAsync()
    {
        var (hasUpdate, url, latest) = await AutoUpdateService.CheckForUpdatesAsync();
        _latestAvailableVersion = hasUpdate ? latest : null;
        _latestDownloadUrl = hasUpdate ? url : null;
        UpdateBannerVisible = hasUpdate;
        UpdateBannerText = hasUpdate ? I18n.T("update_available") + " — " + latest : "";
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

    private void UpdateInfoWindowsText() =>
        InfoWindowsText = I18n.T("windows_os_label") + ": " + OsHelper.GetWindowsDisplayVersion();

    public async Task BootstrapAsync()
    {
        CurrentPage = AppPage.Loading;
        _ = AnalyticsService.SendAnalyticsAsync();
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
            if (logged && token is { Length: > 0 } accessToken && user != null)
            {
                var session = await ApiService.CheckSessionAsync(accessToken);
                if (ApiService.IsSuccess(session))
                {
                    _accessToken = accessToken;
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
        var (ok, error) = await AutoUpdateService.StartUpdateAsync(_latestDownloadUrl);
        if (!ok)
            MessageBox.Show(
                string.IsNullOrEmpty(error)
                    ? I18n.T("update_download_failed")
                    : I18n.T("update_download_failed_detail", ("detail", error)),
                I18n.T("update_app_button"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private async Task GetCodeAsync()
    {
        AuthErrorText = "";
        var mail = Email.Trim();
        if (string.IsNullOrEmpty(mail) || !mail.Contains("@"))
        {
            AuthErrorText = I18n.T("email_hint");
            return;
        }
        var r = await ApiService.AuthAsync(mail);
        if (ApiService.IsSuccess(r))
        {
            _pendingEmail = mail;
            AuthErrorText = "";
            CurrentPage = AppPage.Verify;
        }
        else
        {
            AuthErrorText = r?.TryGetProperty("error", out var er) == true
                ? er.GetString() ?? I18n.T("connection_error")
                : I18n.T("connection_error");
        }
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        AuthErrorText = "";
        if (Code.Trim().Length != 6)
        {
            AuthErrorText = I18n.T("code_6");
            return;
        }
        try
        {
            var deviceId = DeviceIdService.GetWindowsDeviceId();
            var r = await ApiService.VerifyCodeAsync(_pendingEmail, Code.Trim(), deviceId, "VPN Security Connect");
            if (!ApiService.IsSuccess(r) || r!.Value.TryGetProperty("access_token", out var at) == false)
            {
                AuthErrorText = r?.TryGetProperty("error", out var er) == true
                    ? er.GetString() ?? I18n.T("connection_error")
                    : I18n.T("connection_error");
                return;
            }
            var token = at.GetString() ?? "";
            await StorageService.SaveAccessTokenAsync(token);
            await StorageService.SetLoggedInAsync(true);
            await StorageService.SaveUserDataAsync(BuildUserElement(r.Value, _pendingEmail));
            _accessToken = token;
            AuthErrorText = "";
            await EnterMainFlowAsync();
        }
        catch (Exception ex)
        {
            AuthErrorText = ex.Message;
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
    private void OpenSupport()
    {
        try { Process.Start(new ProcessStartInfo("https://vpn-sc.com/support/") { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private void ChangeEmail()
    {
        Email = _pendingEmail;
        Code = "";
        AuthErrorText = "";
        CurrentPage = AppPage.Login;
    }

    partial void OnEmailChanged(string value) => AuthErrorText = "";

    partial void OnCodeChanged(string value) => AuthErrorText = "";

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
        if (_accessToken is not { Length: > 0 } accessToken || IsConnecting || VpnConnected)
            return;

        var uuid = await StorageService.GetUserUuidAsync();
        if (uuid is not { Length: > 0 } userUuid)
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
            wgServerId = _selectedWgServerId;
            if (string.IsNullOrEmpty(wgServerId))
                _wgServerIdByName.TryGetValue(server, out wgServerId);
            if (string.IsNullOrEmpty(wgServerId))
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
            if (!FileManagerService.CheckRequiredFiles())
            {
                var (filesOk, _) = await FileManagerService.DownloadMissingFilesAsync();
                if (!filesOk || !FileManagerService.CheckRequiredFiles())
                {
                    MessageBox.Show(I18n.T("vpn_files_missing"), I18n.T("app_name"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var (ok, err) = await VpnOrchestrator.StartAsync(userUuid, server, accessToken, wgServerId);
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
        if (!VpnConnected && !VpnOrchestrator.IsConnected && !IsConnecting)
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
        if (_wgServerIdByName.TryGetValue(item.RawName, out var wgId))
            _selectedWgServerId = wgId;
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
        if (!UpdateBannerVisible)
            await RefreshUpdateStateAsync();
        if (!UpdateBannerVisible)
            return;

        var latest = _latestAvailableVersion ?? AutoUpdateService.GetCurrentVersion();
        if (MessageBox.Show(I18n.T("update_confirm_message", ("version", latest)),
                I18n.T("update_available"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var (ok, error) = await AutoUpdateService.StartUpdateAsync(_latestDownloadUrl);
        if (!ok)
            MessageBox.Show(
                string.IsNullOrEmpty(error)
                    ? I18n.T("update_download_failed")
                    : I18n.T("update_download_failed_detail", ("detail", error)),
                I18n.T("update_app_button"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (_accessToken is not { Length: > 0 } accessToken)
            return;
        var sub = await ApiService.CheckSubscriptionAsync(accessToken);
        if (sub.ok && sub.hasSubscription)
            await EnterMainFlowAsync();
        else
            MessageBox.Show(I18n.T("subscription_inactive_message"), I18n.T("subscription_inactive_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task TerminateSessionAsync(SessionItemViewModel? session)
    {
        if (_accessToken is not { Length: > 0 } accessToken || session == null ||
            string.IsNullOrEmpty(session.Id) || !session.CanTerminate)
            return;

        var confirm = MessageBox.Show(
            I18n.T("terminate_device_confirm", ("device", session.DeviceName)),
            I18n.T("end_session"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var r = await ApiService.TerminateSessionAsync(accessToken, session.Id);
        if (ApiService.IsSuccess(r))
        {
            await LoadSessionsAsync();
            await LoadProfileAsync();
            MessageBox.Show(I18n.T("session_ended"), I18n.T("device_management"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(
                r?.TryGetProperty("error", out var er) == true
                    ? er.GetString() ?? I18n.T("terminate_device_error")
                    : I18n.T("terminate_device_error"),
                I18n.T("device_management"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public async Task OnClosingAsync()
    {
        try { await VpnOrchestrator.StopAsync(); } catch { /* ignore */ }
    }

    private async Task EnterMainFlowAsync()
    {
        if (_accessToken is not { Length: > 0 } accessToken)
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
        var sub = await ApiService.CheckSubscriptionAsync(accessToken);
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
        await RefreshUpdateStateAsync();
    }

    private async Task LoadServersAsync()
    {
        Servers.Clear();
        _wgServerIdByName.Clear();
        _selectedWgServerId = null;

        var protocol = await StorageService.GetVpnProtocolAsync();
        if (protocol == VpnProtocol.Awg)
        {
            await LoadAwgServersAsync();
            return;
        }

        var uuid = await StorageService.GetUserUuidAsync();
        if (uuid is not { Length: > 0 } userUuid)
            return;

        var (ok, list, err) = await VpnService.GetServersAsync(userUuid);
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
        if (_accessToken is not { Length: > 0 } accessToken)
            return;

        var (ok, list, err) = await AwgVpnService.GetServersAsync(accessToken);
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
            if (SelectedServer != null && _wgServerIdByName.TryGetValue(SelectedServer, out var wgId))
                _selectedWgServerId = wgId;
        }
        else
        {
            SelectedServer = null;
            _selectedWgServerId = null;
        }

        RefreshServerItems();
        UpdateServerDisplay();
    }

    private async Task ReloadServersForProtocolAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return;
        SelectedServer = null;
        _selectedWgServerId = null;
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
            UpdateInfoWindowsText();

            await RefreshUpdateStateAsync();
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
        var protocol = IndexToProtocol(ProtocolIndex);
        await StorageService.SaveVpnProtocolAsync(protocol);
        UpdateProtocolDisplay();

        if (!string.IsNullOrEmpty(_accessToken))
            await ReloadServersForProtocolAsync();
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
        IsProfileLoading = true;
        try
        {
            if (_accessToken is not { Length: > 0 } accessToken)
            {
                ResetProfileView();
                return;
            }

            var json = await ApiService.GetUserInfoAsync(accessToken);
            if (json != null && ApiService.IsSuccess(json) && json.Value.TryGetProperty("user", out var user))
                FillProfileFromUser(user);
            else
            {
                var stored = await StorageService.GetUserDataAsync();
                if (stored != null)
                    FillProfileFromUser(stored.Value);
                else
                    ResetProfileView();
            }

            var count = await CountActiveSessionsAsync(accessToken);
            UpdateDeviceManagementSubText(count);

            if (string.IsNullOrEmpty(ProfileIdText))
                ProfileIdText = FormatProfileIdText(await StorageService.GetUserUuidAsync());
        }
        catch
        {
            ResetProfileView();
        }
        finally
        {
            IsProfileLoading = false;
        }
    }

    private void ResetProfileView()
    {
        ProfileEmail = "—";
        ProfileSubscriptionText = "";
        ProfileIdText = "";
        ProfileSubscriptionActive = false;
        ProfileSubscriptionBadgeText = I18n.T("subscription_inactive_title");
        UpdateDeviceManagementSubText(0);
    }

    private void FillProfileFromUser(JsonElement user)
    {
        ProfileEmail = (user.TryGetProperty("mail", out var m) ? m.GetString()
            : user.TryGetProperty("email", out var e) ? e.GetString() : null) ?? "—";
        ProfileIdText = user.TryGetProperty("uuid", out var uuidEl)
            ? FormatProfileIdText(uuidEl.GetString())
            : "";
        var days = 0;
        if (user.TryGetProperty("subscription_days", out var sd))
        {
            if (sd.ValueKind == JsonValueKind.Number && sd.TryGetInt32(out var n))
                days = n;
            else if (sd.ValueKind == JsonValueKind.String && int.TryParse(sd.GetString(), out var ns))
                days = ns;
        }

        ProfileSubscriptionText = days > 0
            ? I18n.T("subscription_active_until", ("date", I18n.FormatSubscriptionEndDate(days)))
            : I18n.T("subscription_inactive_message");
        ProfileSubscriptionActive = days > 0;
        ProfileSubscriptionBadgeText = days > 0
            ? I18n.T("subscription_active_badge")
            : I18n.T("subscription_inactive_title");
    }

    private static string FormatProfileIdText(string? uuid)
    {
        if (uuid is not { Length: > 0 } value)
            return "";
        var parts = value.Split('-');
        if (parts.Length == 0)
            return "";
        var last = parts[parts.Length - 1];
        if (string.IsNullOrWhiteSpace(last))
            return "";
        return I18n.T("id_label") + ": " + last;
    }

    private async Task LoadSessionsAsync()
    {
        SessionItems.Clear();
        SessionsEmpty = true;
        if (_accessToken is not { Length: > 0 } accessToken)
            return;

        JsonElement? json;
        try
        {
            json = await ApiService.GetSessionsListAsync(accessToken);
        }
        catch
        {
            MessageBox.Show(I18n.T("devices_load_error"), I18n.T("device_management"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (json == null || !ApiService.IsSuccess(json) ||
            !json.Value.TryGetProperty("active_sessions", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                continue;
            SessionItems.Add(CreateSessionItem(el));
        }

        SessionsEmpty = SessionItems.Count == 0;
        UpdateDeviceManagementSubText(SessionItems.Count);
    }

    private static SessionItemViewModel CreateSessionItem(JsonElement el)
    {
        var id = "";
        if (el.TryGetProperty("id", out var idP))
            id = idP.ValueKind == JsonValueKind.String ? idP.GetString() ?? "" : idP.GetRawText();

        var name = el.TryGetProperty("device_name", out var dn) ? dn.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(name))
            name = I18n.T("unknown_device");

        var platformKey = el.TryGetProperty("platform", out var pl) ? pl.GetString() ?? "" : "";
        // IP скрыт из UI; раскомментировать при возврате поля в карточку устройства.
        // var ip = el.TryGetProperty("ip_address", out var ipP) ? ipP.GetString() ?? "" : "";
        // if (string.IsNullOrWhiteSpace(ip))
        //     ip = I18n.T("unknown");
        var ip = string.Empty;

        var created = el.TryGetProperty("created_at", out var cr) ? cr.GetString() : null;
        var lastActivity = el.TryGetProperty("last_activity", out var la) ? la.GetString() : null;
        var status = el.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
        var isActive = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
        var isCurrent = IsCurrentDeviceName(name);

        return new SessionItemViewModel
        {
            Id = id,
            DeviceName = name,
            PlatformKey = platformKey,
            PlatformText = FormatPlatform(platformKey),
            IpAddress = ip,
            CreatedText = FormatSessionDate(created),
            LastActivityText = FormatSessionDate(lastActivity),
            HasLastActivity = !string.IsNullOrWhiteSpace(lastActivity),
            IsCurrentDevice = isCurrent,
            IsActive = isActive,
            CanTerminate = !isCurrent,
            // IpLabel = I18n.T("ip_address"),
            IpLabel = string.Empty,
            CreatedLabel = I18n.T("created"),
            LastActivityLabel = I18n.T("last_activity"),
            CurrentDeviceText = I18n.T("current"),
            TerminateButtonText = I18n.T("end_session")
        };
    }

    private static bool IsCurrentDeviceName(string? name) =>
        string.Equals(name, "VPN Security Connect", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Windows VPN Client", StringComparison.OrdinalIgnoreCase);

    private static string FormatPlatform(string? platform) =>
        (platform ?? "").ToLowerInvariant() switch
        {
            "android" => "Android",
            "ios" => "iOS",
            "app" => "Windows",
            _ => string.IsNullOrWhiteSpace(platform) ? I18n.T("unknown") : platform!
        };

    private static string FormatSessionDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return I18n.T("unknown");
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ||
            DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
            return dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
        return raw!;
    }

    private void UpdateDeviceManagementSubText(int count) =>
        DeviceManagementSubText = count > 0
            ? I18n.T("active_devices_count", ("count", count))
            : I18n.T("device_management_subtitle");

    private async Task<int> CountActiveSessionsAsync(string accessToken)
    {
        try
        {
            var json = await ApiService.GetSessionsListAsync(accessToken);
            if (json == null || !ApiService.IsSuccess(json) ||
                !json.Value.TryGetProperty("active_sessions", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return 0;
            return arr.GetArrayLength();
        }
        catch
        {
            return 0;
        }
    }

    private async Task RefreshStatsAsync()
    {
        if (Interlocked.Exchange(ref _statsRefreshInProgress, 1) == 1)
            return;

        try
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
                _tunnelHealthFailures = 0;
                TrafficUplinkText = "";
                TrafficDownlinkText = "";
                return;
            }

            await CheckTunnelHealthAsync();

            var uuid = await StorageService.GetUserUuidAsync() ?? "";
            var (_, connected, stats) = await VpnOrchestrator.GetStatusAsync(uuid);
            if (stats != null)
            {
                if (stats.TryGetPropertyValue("uplink", out var up) && up!.GetValue<long>() >= 0)
                    TrafficUplinkText = "↑ " + FormatBytes(up.GetValue<long>());
                if (stats.TryGetPropertyValue("downlink", out var down) && down!.GetValue<long>() >= 0)
                    TrafficDownlinkText = "↓ " + FormatBytes(down.GetValue<long>());
            }
        }
        finally
        {
            Interlocked.Exchange(ref _statsRefreshInProgress, 0);
        }
    }

    private async Task CheckTunnelHealthAsync()
    {
        if (!VpnConnected || IsConnecting)
            return;

        var protocol = await StorageService.GetVpnProtocolAsync();
        var isAwg = protocol == VpnProtocol.Awg ||
                    (protocol == VpnProtocol.Auto && VpnSessionService.ActiveStack == VpnActiveStack.Awg);

        var tunnelOk = isAwg
            ? await VpnTunnelProbe.TestInternetAsync()
            : await VpnTunnelProbe.MeasureThroughSocksAsync();

        if (tunnelOk)
        {
            _tunnelHealthFailures = 0;
            return;
        }

        _tunnelHealthFailures++;
        if (_tunnelHealthFailures < 3)
            return;

        _tunnelHealthFailures = 0;
        await OnTunnelLostAsync();
    }

    private async Task OnTunnelLostAsync()
    {
        if (!VpnConnected)
            return;

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
        MessageBox.Show(I18n.T("no_internet"), I18n.T("app_name"),
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void UpdateConnectionStatusText() =>
        ConnectionStatusText = IsConnecting
            ? I18n.T("connecting")
            : VpnConnected ? I18n.T("connected") : I18n.T("disconnected");

    private void UpdateServerDisplay()
    {
        var name = SelectedServer;
        if (name is { Length: > 0 } serverName)
        {
            ServerIsSmartLocation = FlagService.IsSmartLocation(serverName);
            ServerFlagImage = FlagService.GetFlagImage(serverName);
            PickerServerName = FlagService.GetCountryName(serverName);
        }
        else
        {
            ServerIsSmartLocation = false;
            ServerFlagImage = null;
            PickerServerName = SelectServerText;
        }

        ConnectedServerLabel = VpnConnected && _connectedServerRaw is { Length: > 0 } connectedServer
            ? I18n.T("server_label", ("server", connectedServer))
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
        if (bytes < 0)
            return "—";
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
