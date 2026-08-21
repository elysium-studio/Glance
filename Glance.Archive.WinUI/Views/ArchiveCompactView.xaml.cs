using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Archive.WinUI;

public sealed partial class ArchiveCompactView :
    UserControl
{
    public ArchiveCompactView(ArchiveViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ArchiveViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
