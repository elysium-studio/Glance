using Glance.Spotify;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyExpandedView :
    UserControl
{
    private EventHandler<object>? firstFrameHandler;

    public SpotifyExpandedView(SpotifyViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SpotifyViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        TitleText.IsMarqueeEnabled = false;
        firstFrameHandler = (_, _) =>
        {
            CompositionTarget.Rendering -= firstFrameHandler;
            firstFrameHandler = null;
            TitleText.IsMarqueeEnabled = true;
        };
        CompositionTarget.Rendering += firstFrameHandler;
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        if (firstFrameHandler is not null)
        {
            CompositionTarget.Rendering -= firstFrameHandler;
            firstFrameHandler = null;
        }
    }

    private void HandleProgressChanged(object sender, PointerRoutedEventArgs args) =>
        ViewModel.Seek(ProgressSlider.Value);

    private void HandlePlaybackSourceFlyoutOpening(object sender, object args)
    {
        PlaybackSourceFlyout.Items.Clear();

        foreach (SpotifyDevice device in ViewModel.Devices)
        {
            RadioMenuFlyoutItem item = new()
            {
                Text = device.Name,
                Tag = device.Id,
                GroupName = "SpotifyPlaybackSources",
                IsChecked = string.Equals(device.Id, ViewModel.SelectedDeviceId, StringComparison.Ordinal)
            };
            item.Click += HandlePlaybackSourceClicked;
            PlaybackSourceFlyout.Items.Add(item);
        }
    }

    private void HandlePlaybackSourceClicked(object sender, RoutedEventArgs args)
    {
        if (sender is RadioMenuFlyoutItem { Tag: string deviceId })
        {
            ViewModel.Transfer(deviceId);
        }
    }
}
