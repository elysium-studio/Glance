using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.PrivacyControls.WinUI;

public sealed partial class PrivacyControlsCompactView :
    UserControl
{
    public PrivacyControlsCompactView(PrivacyControlsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public PrivacyControlsViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
