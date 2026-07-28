using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Stash.WinUI;

public sealed partial class StashCompactView :
    UserControl
{
    public StashCompactView(StashViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public StashViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
