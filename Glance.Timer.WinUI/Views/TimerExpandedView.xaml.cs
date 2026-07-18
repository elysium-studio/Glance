using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerExpandedView : UserControl
{
    public TimerExpandedView(TimerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
