using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.QuickConvert.WinUI;

public sealed partial class QuickConvertExpandedView :
    UserControl
{
    private readonly CompositionActivityPulse activityPulse;
    private readonly ModuleResourceTextLocalizer<QuickConvertModule> localizer;
    private readonly Action stopConversions;

    public QuickConvertExpandedView(QuickConvertViewModel viewModel,
        ModuleResourceTextLocalizer<QuickConvertModule> localizer,
        Action stopConversions)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        this.stopConversions = stopConversions;
        InitializeComponent();
        activityPulse = new(this, PulseRing, viewModel, nameof(QuickConvertViewModel.IsBusy), () => viewModel.IsBusy);
    }

    public QuickConvertViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string StopConversionsLabel => localizer.GetText("StopConversions");

    private void StopConversions() => stopConversions();

    private string ToActionGlyph(bool isBusy) => isBusy ? "\uF8AE" : "\uF5B0";

    private string ToUpper(string value) => value.ToUpperInvariant();
}
