using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class ScreenCaptureCompactView : UserControl
{
    public ScreenCaptureCompactView(ScreenCaptureViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenCaptureViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
