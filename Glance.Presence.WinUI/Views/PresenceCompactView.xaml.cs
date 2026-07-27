using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceCompactView :
    UserControl
{
    public PresenceCompactView(PresenceViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public PresenceViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
