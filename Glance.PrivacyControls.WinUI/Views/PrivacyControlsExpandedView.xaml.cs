using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.PrivacyControls.WinUI;

public sealed partial class PrivacyControlsExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<PrivacyControlsModule> localizer;

    public PrivacyControlsExpandedView(
        PrivacyControlsViewModel viewModel,
        ModuleResourceTextLocalizer<PrivacyControlsModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public PrivacyControlsViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) =>
        value.ToUpperInvariant();

    private Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;
}
