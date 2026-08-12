using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;

namespace Glance.Network.WinUI;

public sealed partial class NetworkAdapterExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<NetworkModule> localizer;

    public NetworkAdapterExpandedView(NetworkAdapterViewModel viewModel,
        ModuleResourceTextLocalizer<NetworkModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public NetworkAdapterViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("NetworkAdapterDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenEmpty(bool hasAdapter) => hasAdapter ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasAdapter) => hasAdapter ? Visibility.Visible : Visibility.Collapsed;

    private async void HandleOpenNetworkSettingsClicked(object sender,
        RoutedEventArgs args)
    {
        try
        {
            _ = await Launcher.LaunchUriAsync(new Uri("ms-settings:network-status"));
        }
        catch
        {
        }
    }
}
