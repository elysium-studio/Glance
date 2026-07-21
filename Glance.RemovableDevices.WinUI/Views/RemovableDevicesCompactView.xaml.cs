using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.RemovableDevices.WinUI;

public sealed partial class RemovableDevicesCompactView :
    UserControl
{
    public RemovableDevicesCompactView(RemovableDevicesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public RemovableDevicesViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
