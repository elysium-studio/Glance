using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class ScreenCaptureExpandedView : UserControl
{
    public ScreenCaptureExpandedView(ScreenCaptureViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenCaptureViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private bool WhenIdle(bool isCapturing) => !isCapturing;

    private Visibility WhenEmpty(bool hasCaptures) =>
        hasCaptures ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasCaptures) =>
        hasCaptures ? Visibility.Visible : Visibility.Collapsed;
}
