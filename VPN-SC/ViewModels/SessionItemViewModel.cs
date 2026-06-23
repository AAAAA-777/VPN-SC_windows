namespace VpnSc.ViewModels;

public sealed class SessionItemViewModel
{
    public string Id { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string PlatformKey { get; init; } = "";
    public string PlatformText { get; init; } = "";
    public string IpAddress { get; init; } = "";
    public string CreatedText { get; init; } = "";
    public string LastActivityText { get; init; } = "";
    public bool HasLastActivity { get; init; }
    public bool IsCurrentDevice { get; init; }
    public bool IsActive { get; init; }
    public bool CanTerminate { get; init; }
    public string IpLabel { get; init; } = "";
    public string CreatedLabel { get; init; } = "";
    public string LastActivityLabel { get; init; } = "";
    public string CurrentDeviceText { get; init; } = "";
    public string TerminateButtonText { get; init; } = "";
}
