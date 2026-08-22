using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Fasting.WinUI;

public sealed partial class FastingCompactView :
    UserControl
{
    public FastingCompactView(FastingViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public FastingViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
