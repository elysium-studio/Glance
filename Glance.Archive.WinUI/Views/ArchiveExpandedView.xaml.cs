using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Archive.WinUI;

public sealed partial class ArchiveExpandedView :
    UserControl
{
    private readonly CompositionActivityPulse activityPulse;
    private readonly ModuleResourceTextLocalizer<ArchiveModule> localizer;
    private readonly Action stop;

    public ArchiveExpandedView(ArchiveViewModel viewModel, ModuleResourceTextLocalizer<ArchiveModule> localizer, Action stop)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        this.stop = stop;
        InitializeComponent();
        activityPulse = new(this, PulseRing, viewModel, nameof(ArchiveViewModel.IsBusy), () => viewModel.IsBusy);
    }

    public ArchiveViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string StopLabel => localizer.GetText("StopArchiveOperation");

    private void Stop() => stop();

    private string ToUpper(string value) => value.ToUpperInvariant();
}
