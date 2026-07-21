using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<StopwatchModule> localizer;

    public StopwatchExpandedView(
        StopwatchViewModel viewModel,
        ModuleResourceTextLocalizer<StopwatchModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public StopwatchViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
