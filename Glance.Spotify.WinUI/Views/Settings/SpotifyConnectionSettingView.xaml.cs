using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyConnectionSettingView : UserControl
{
    public SpotifyConnectionSettingView() => InitializeComponent();

    public SpotifyConnectionSettingViewModel ViewModel => (SpotifyConnectionSettingViewModel)DataContext;

    private async void HandleConnectionClicked(object sender, RoutedEventArgs args) =>
        await ViewModel.ChangeConnectionAsync();
}
