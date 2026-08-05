using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class ScreenRecorderCompactView :
    UserControl
{
    public ScreenRecorderCompactView(ScreenRecorderViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenRecorderViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
