using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationCompactView :
    UserControl
{
    public HydrationCompactView(HydrationViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public HydrationViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
