using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Network.WinUI;

public sealed partial class NetworkAdapterCompactView :
    UserControl
{
    public NetworkAdapterCompactView(NetworkAdapterViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public NetworkAdapterViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
