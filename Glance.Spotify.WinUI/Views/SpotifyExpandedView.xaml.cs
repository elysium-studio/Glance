using Elysium.UI.Controls.WinUI;
using Glance.Spotify;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyExpandedView :
    UserControl
{
    private EventHandler<object>? firstFrameHandler;
    private DesktopIsland? playbackSourceExpansionIsland;

    public SpotifyExpandedView(SpotifyViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ProgressSlider.ThumbToolTipValueConverter = new TrackPositionConverter();
    }

    public SpotifyViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => ArtworkContainer;

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

        if (playbackSourceExpansionIsland is not null)
        {
            DesktopIsland island = playbackSourceExpansionIsland;
            DetachPlaybackSourceExpansionIsland();
            island.IsExpansionLocked = false;
        }
    }

    private void HandleProgressChanged(object sender, PointerRoutedEventArgs args) =>
        ViewModel.Seek(ProgressSlider.Value);

    private void HandlePlaybackSourceFlyoutOpening(object sender, object args)
    {
        PlaybackSourceFlyout.Items.Clear();

        foreach (SpotifyDevice device in ViewModel.Devices)
        {
            ToggleMenuFlyoutItem item = new()
            {
                Text = device.Name,
                Tag = device.Id,
                IsChecked = string.Equals(device.Id, ViewModel.SelectedDeviceId, StringComparison.Ordinal)
            };
            item.Click += HandlePlaybackSourceClicked;
            PlaybackSourceFlyout.Items.Add(item);
        }
    }

    private void HandlePlaybackSourceFlyoutOpened(object sender, object args) =>
        SetPlaybackSourceExpansionLocked(true);

    private void HandlePlaybackSourceFlyoutClosed(object sender, object args) =>
        ReleasePlaybackSourceExpansionLock();

    private void HandlePlaybackSourceClicked(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleMenuFlyoutItem { Tag: string deviceId })
        {
            ViewModel.Transfer(deviceId);
        }
    }

    private void SetPlaybackSourceExpansionLocked(bool isLocked)
    {
        DesktopIsland? island = FindIsland();

        if (island is null)
        {
            return;
        }

        DetachPlaybackSourceExpansionIsland();
        island.IsExpansionLocked = isLocked;
        playbackSourceExpansionIsland = isLocked ? island : null;
    }

    private void ReleasePlaybackSourceExpansionLock()
    {
        DesktopIsland? island = playbackSourceExpansionIsland ?? FindIsland();

        if (island is null)
        {
            return;
        }

        if (island.IsPointerWithinInteractiveRegion)
        {
            playbackSourceExpansionIsland = island;
            island.PointerExited -= HandlePlaybackSourceExpansionIslandPointerExited;
            island.PointerExited += HandlePlaybackSourceExpansionIslandPointerExited;
            return;
        }

        DetachPlaybackSourceExpansionIsland();
        island.IsExpansionLocked = false;
    }

    private void HandlePlaybackSourceExpansionIslandPointerExited(object sender,
        PointerRoutedEventArgs args)
    {
        DesktopIsland island = (DesktopIsland)sender;
        DetachPlaybackSourceExpansionIsland();
        island.IsExpansionLocked = false;
    }

    private void DetachPlaybackSourceExpansionIsland()
    {
        playbackSourceExpansionIsland?.PointerExited -= HandlePlaybackSourceExpansionIslandPointerExited;
        playbackSourceExpansionIsland = null;
    }

    private DesktopIsland? FindIsland()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is DesktopIsland island)
            {
                return island;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private string ToUpper(string value) => value.ToUpperInvariant();

    private ImageSource? ToImageSource(object? value) => value as ImageSource;

    private SolidColorBrush ToAccentBrush(uint color) => SpotifyAccentPalette.GetBrush(color);

    private Color ToAccentColor(uint color) => SpotifyAccentPalette.GetAccent(color);

    private Color ToPointerOverColor(uint color) => SpotifyAccentPalette.GetPointerOver(color);

    private Color ToPressedColor(uint color) => SpotifyAccentPalette.GetPressed(color);

    private Color ToDisabledColor(uint color) => SpotifyAccentPalette.GetDisabled(color);

    private Color ToForegroundColor(uint color) => SpotifyAccentPalette.GetForeground(color);

    private Color ToPointerOverForegroundColor(uint color) => SpotifyAccentPalette.GetPointerOverForeground(color);

    private Color ToPressedForegroundColor(uint color) => SpotifyAccentPalette.GetPressedForeground(color);

    private Color ToDisabledForegroundColor(uint color) => SpotifyAccentPalette.GetDisabledForeground(color);

    private Color ToBorderColor(uint color) => SpotifyAccentPalette.GetBorder(color);

    private Color ToPointerOverBorderColor(uint color) => SpotifyAccentPalette.GetPointerOverBorder(color);

    private Color ToPressedBorderColor(uint color) => SpotifyAccentPalette.GetPressedBorder(color);

    private Color ToDisabledBorderColor(uint color) => SpotifyAccentPalette.GetDisabledBorder(color);

    private Visibility WhenArtworkUnavailable(object? artwork) => artwork is null ? Visibility.Visible : Visibility.Collapsed;

    private sealed class TrackPositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double milliseconds = value is double position ? position : 0;
            TimeSpan time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
            return time.TotalHours >= 1
                ? time.ToString(@"h\:mm\:ss")
                : time.ToString(@"m\:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
