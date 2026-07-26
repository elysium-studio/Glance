using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.KeepAwake.WinUI;

public sealed partial class KeepAwakeCompactView :
    UserControl
{
    public KeepAwakeCompactView(KeepAwakeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public KeepAwakeViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
