using Glance.Spotify;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyCompactView :
    UserControl
{
    private EventHandler<object>? firstFrameHandler;

    public SpotifyCompactView(SpotifyViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SpotifyViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => LogoContainer;

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
}
