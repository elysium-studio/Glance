using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorCompactView : UserControl
{
    public SystemMonitorCompactView(SystemMonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
