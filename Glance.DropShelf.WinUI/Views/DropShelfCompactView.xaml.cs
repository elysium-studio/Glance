using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.DropShelf.WinUI;

public sealed partial class DropShelfCompactView : UserControl
{
    public DropShelfCompactView(DropShelfViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public DropShelfViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
