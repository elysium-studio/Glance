using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusSessionExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<FocusSessionModule> localizer;

    public FocusSessionExpandedView(
        FocusSessionViewModel viewModel,
        ModuleResourceTextLocalizer<FocusSessionModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public FocusSessionViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToPhaseName(FocusSessionPhase phase) =>
        localizer.GetText(phase == FocusSessionPhase.Focus
            ? "FocusPhase"
            : "BreakPhase");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
