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
    private Visibility WhenEmpty(bool hasTorrents) => hasTorrents ? Visibility.Collapsed : Visibility.Visible;
    private Visibility WhenPopulated(bool hasTorrents) => hasTorrents ? Visibility.Visible : Visibility.Collapsed;
}
