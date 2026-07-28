using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusSessionExpandedView :
    UserControl
{
    private readonly CompositionActivityPulse activityPulse;
    private readonly ModuleResourceTextLocalizer<FocusSessionModule> localizer;

    public FocusSessionExpandedView(FocusSessionViewModel viewModel,
        ModuleResourceTextLocalizer<FocusSessionModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
        activityPulse = new(this, PulseRing, viewModel, nameof(FocusSessionViewModel.IsRunning), () => viewModel.IsRunning);
    }

    public FocusSessionViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
