using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VpnSc.Localization;
using VpnSc.Services;

namespace VpnSc.Windows;

public partial class ServerSelectionWindow
{
    public string? SelectedServer { get; private set; }

    public ServerSelectionWindow(IReadOnlyList<string> servers, string? current)
    {
        InitializeComponent();
        TxtTitle.Text = I18n.T("select_server");
        var items = servers.Select(s => new ServerRow
        {
            RawName = s,
            DisplayName = FlagService.GetCountryName(s),
            FlagImage = FlagService.GetFlagImage(s),
            IsSmartLocation = FlagService.IsSmartLocation(s),
            ShowRecommended = FlagService.IsSmartLocation(s),
            RecommendedText = I18n.T("recommended")
        }).ToList();
        ServerList.ItemsSource = items;
        if (!string.IsNullOrEmpty(current))
        {
            var match = items.FirstOrDefault(i => i.RawName == current);
            if (match != null)
                ServerList.SelectedItem = match;
        }
        else if (items.Count > 0)
            ServerList.SelectedItem = items[0];
    }

    private void ServerList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServerList.SelectedItem is not ServerRow row)
            return;
        SelectedServer = row.RawName;
        DialogResult = true;
        Close();
    }

    private sealed class ServerRow
    {
        public string RawName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public ImageSource? FlagImage { get; init; }
        public bool IsSmartLocation { get; init; }
        public bool ShowRecommended { get; init; }
        public string RecommendedText { get; init; } = "";
    }
}
