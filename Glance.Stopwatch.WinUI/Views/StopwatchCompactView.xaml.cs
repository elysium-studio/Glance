using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchCompactView : UserControl
{
    public StopwatchCompactView(StopwatchViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public StopwatchViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
