using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerExpandedView : 
    UserControl
{
    public TimerExpandedView(TimerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public TimerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();
}
