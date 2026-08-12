using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Torrents.WinUI;

public sealed partial class TorrentCompactView : UserControl
{
    public TorrentCompactView(TorrentsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public TorrentsViewModel ViewModel { get; }
    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
