using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.DevicePresence.WinUI;

public sealed partial class DevicePresenceCompactView :
    UserControl
{
    public DevicePresenceCompactView(DevicePresenceViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public DevicePresenceViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
