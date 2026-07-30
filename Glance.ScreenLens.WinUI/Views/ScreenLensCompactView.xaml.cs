using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenLens.WinUI;

public sealed partial class ScreenLensCompactView :
    UserControl
{
    public ScreenLensCompactView(ScreenLensViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenLensViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
