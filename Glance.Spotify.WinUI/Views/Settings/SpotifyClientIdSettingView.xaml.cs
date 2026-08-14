using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyClientIdSettingView : UserControl
{
    public SpotifyClientIdSettingView() => InitializeComponent();

    public SpotifyClientIdSettingViewModel ViewModel => (SpotifyClientIdSettingViewModel)DataContext;

    private async void HandleOpenDashboard(object sender, RoutedEventArgs args) =>
        await ViewModel.OpenDashboardAsync();

    private async void HandleCopyRedirectUri(object sender, RoutedEventArgs args) =>
        _ = await ViewModel.CopyRedirectUriAsync();
}
