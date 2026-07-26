using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Infinity.WinUI;

public sealed partial class InfinityCompactView :
    UserControl
{
    public InfinityCompactView(InfinityViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public InfinityViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToDisplayText(bool isConnected, string pageTitle) => isConnected
        ? pageTitle
        : string.Empty;
}
