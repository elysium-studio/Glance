using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Glance.Media.WinUI;

public sealed partial class MediaCompactView : UserControl
{
    public MediaCompactView(MediaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public FrameworkElement ConnectedAnimationElement => ArtworkContainer;

    private MediaViewModel ViewModel => (MediaViewModel)DataContext;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged += HandlePropertyChanged;
        UpdatePlaybackMotion();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= HandlePropertyChanged;
        FluentMotion.StopActivityPulse(ArtworkContainer);
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MediaViewModel.IsPlaying))
        {
            UpdatePlaybackMotion();
        }
    }

    private void UpdatePlaybackMotion()
    {
        if (ViewModel.IsPlaying)
        {
            FluentMotion.StartActivityPulse(ArtworkContainer, 1.035f, 2600);
        }
        else
        {
            FluentMotion.StopActivityPulse(ArtworkContainer);
        }
    }
}
