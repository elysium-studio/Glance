using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorCompactView : UserControl
{
    public SystemMonitorCompactView(SystemMonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private void HandleLoaded(object sender, RoutedEventArgs args) =>
        FluentMotion.StartActivityPulse(StatusIndicator, 1.025f, 3200);

    private void HandleUnloaded(object sender, RoutedEventArgs args) =>
        FluentMotion.StopActivityPulse(StatusIndicator);
}
