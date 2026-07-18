using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerCompactView : UserControl
{
    public TimerCompactView(TimerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
