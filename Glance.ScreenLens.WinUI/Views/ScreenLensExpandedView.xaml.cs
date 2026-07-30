using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenLens.WinUI;

public sealed partial class ScreenLensExpandedView :
    UserControl
{
    public ScreenLensExpandedView(ScreenLensViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenLensViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private bool WhenIdle(bool isExtracting) => !isExtracting;
}
