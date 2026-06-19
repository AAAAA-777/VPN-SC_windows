using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VpnSc.ViewModels;

public partial class ServerItemViewModel : ObservableObject
{
    public string RawName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public ImageSource? FlagImage { get; init; }
    public bool IsSmartLocation { get; init; }
    public bool ShowRecommended { get; init; }
    public string RecommendedText { get; init; } = "";

    [ObservableProperty] private bool _isSelected;
}
