using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace Glance.Media.WinUI;

public sealed partial class MediaCompactView :
    UserControl
{
    private EventHandler<object>? firstFrameHandler;

    public MediaCompactView(MediaViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MediaViewModel ViewModel { get; }

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
    }

    private ImageSource? ToImageSource(object? value) => value as ImageSource;

    private Visibility WhenArtworkUnavailable(object? artwork) =>
        artwork is null ? Visibility.Visible : Visibility.Collapsed;

    private Visibility WhenAvailable(bool isAvailable) =>
        isAvailable ? Visibility.Visible : Visibility.Collapsed;
}
