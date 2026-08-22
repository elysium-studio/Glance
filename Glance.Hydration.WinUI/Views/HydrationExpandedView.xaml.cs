using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationExpandedView :
    UserControl
{
    public HydrationExpandedView(HydrationViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public HydrationViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();
}
