using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Glance.Timer.WinUI;

public sealed partial class TimerExpandedView : UserControl
{
    public TimerExpandedView(TimerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private TimerViewModel ViewModel => (TimerViewModel)DataContext;

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
        if (args.PropertyName == nameof(TimerViewModel.IsRunning))
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
