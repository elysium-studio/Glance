using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Power.WinUI;

public sealed partial class PowerCompactView : UserControl
{
    public PowerCompactView(PowerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public PowerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
