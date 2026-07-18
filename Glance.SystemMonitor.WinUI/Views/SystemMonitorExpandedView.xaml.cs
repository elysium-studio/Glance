using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorExpandedView : UserControl
{
    public SystemMonitorExpandedView(SystemMonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
