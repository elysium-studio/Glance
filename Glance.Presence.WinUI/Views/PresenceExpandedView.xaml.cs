using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<PresenceModule> localizer;

    public PresenceExpandedView(PresenceViewModel viewModel,
        ModuleResourceTextLocalizer<PresenceModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public PresenceViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private bool IsActionEnabled(bool isBusy) =>
        !isBusy;

    private string ToUpper(string value) =>
        value.ToUpperInvariant();
}
