using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class SystemMonitorExpandedView : UserControl
{
    private readonly ModuleResourceTextLocalizer<SystemMonitorModule> localizer;

    public SystemMonitorExpandedView(
        SystemMonitorViewModel viewModel,
        ModuleResourceTextLocalizer<SystemMonitorModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public SystemMonitorViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
