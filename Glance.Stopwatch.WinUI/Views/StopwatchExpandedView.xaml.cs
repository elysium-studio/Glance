using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchExpandedView : UserControl
{
    public StopwatchExpandedView(StopwatchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
