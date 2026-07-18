using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardCompactView : UserControl
{
    public ClipboardCompactView(ClipboardShelfViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
