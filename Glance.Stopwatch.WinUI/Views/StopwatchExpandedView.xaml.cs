using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchExpandedView : UserControl
{
    public StopwatchExpandedView(StopwatchViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public StopwatchViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();
}
