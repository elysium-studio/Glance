using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockCompactView :
    UserControl
{
    public WorldClockCompactView(WorldClockViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public WorldClockViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
