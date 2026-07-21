using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<TimerModule> localizer;

    public TimerExpandedView(
        TimerViewModel viewModel,
        ModuleResourceTextLocalizer<TimerModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public TimerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
