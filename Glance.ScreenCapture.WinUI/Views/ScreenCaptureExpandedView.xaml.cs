using Elysium.UI.Controls.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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

    private void HandleCaptureMenuOpened(object sender, object args) =>
        SetExpansionLocked(true);

    private void HandleCaptureMenuClosed(object sender, object args) =>
        SetExpansionLocked(false);

    private void SetExpansionLocked(bool isLocked)
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is DesktopIsland island)
            {
                island.IsExpansionLocked = isLocked;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private string ToUpper(string value) => value.ToUpperInvariant();

    private bool WhenIdle(bool isCapturing) => !isCapturing;

    private Visibility WhenEmpty(bool hasCaptures) =>
        hasCaptures ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasCaptures) =>
        hasCaptures ? Visibility.Visible : Visibility.Collapsed;
}
