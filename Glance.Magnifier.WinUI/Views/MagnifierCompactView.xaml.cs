using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Magnifier.WinUI;

public sealed partial class MagnifierCompactView :
    UserControl
{
    public MagnifierCompactView(MagnifierViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MagnifierViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
