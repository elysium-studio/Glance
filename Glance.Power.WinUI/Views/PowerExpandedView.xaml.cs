using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Power.WinUI;

public sealed partial class PowerExpandedView : UserControl
{
    public PowerExpandedView(PowerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public PowerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenPresent(bool hasBattery) =>
        hasBattery ? Visibility.Visible : Visibility.Collapsed;
}
