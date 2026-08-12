using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Network.WinUI;

public sealed partial class NetworkCompactView :
    UserControl
{
    public NetworkCompactView(NetworkViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public NetworkViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
