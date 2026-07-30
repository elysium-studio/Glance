using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenLens.WinUI;

public sealed partial class ScreenLensExpandedView :
    UserControl
{
    public ScreenLensExpandedView(ScreenLensViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ScreenLensViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string DisplayText(bool hasText, string text, string status) =>
        hasText ? text : status;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenTextAvailable(bool hasText) =>
        hasText ? Visibility.Visible : Visibility.Collapsed;

    private bool WhenIdle(bool isExtracting) => !isExtracting;
}
