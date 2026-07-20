using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorCompactView : UserControl
{
    public SystemMonitorCompactView(SystemMonitorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SystemMonitorViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
