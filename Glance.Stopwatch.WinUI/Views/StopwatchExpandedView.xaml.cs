using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchExpandedView : UserControl
{
    public StopwatchExpandedView(StopwatchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private StopwatchViewModel ViewModel => (StopwatchViewModel)DataContext;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged += HandlePropertyChanged;
        UpdateActivityMotion();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= HandlePropertyChanged;
        FluentMotion.StopActivityPulse(StatusIndicator);
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StopwatchViewModel.IsRunning))
        {
            UpdateActivityMotion();
        }
    }

    private void UpdateActivityMotion()
    {
        if (ViewModel.IsRunning)
        {
            FluentMotion.StartActivityPulse(StatusIndicator, 1.06f, 1800);
        }
        else
        {
            FluentMotion.StopActivityPulse(StatusIndicator);
        }
    }
}
