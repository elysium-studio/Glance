using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<WorldClockModule> localizer;

    public WorldClockExpandedView(WorldClockViewModel viewModel,
        ModuleResourceTextLocalizer<WorldClockModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public WorldClockViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
