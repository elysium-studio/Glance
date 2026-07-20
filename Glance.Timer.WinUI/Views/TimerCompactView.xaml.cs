using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerCompactView : 
    UserControl
{
    public TimerCompactView(TimerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public TimerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
