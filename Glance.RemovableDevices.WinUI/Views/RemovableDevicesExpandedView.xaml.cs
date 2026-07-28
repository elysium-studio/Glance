using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.RemovableDevices.WinUI;

public sealed partial class RemovableDevicesExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<RemovableDevicesModule> localizer;

    public RemovableDevicesExpandedView(RemovableDevicesViewModel viewModel,
        ModuleResourceTextLocalizer<RemovableDevicesModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public RemovableDevicesViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) =>
        value.ToUpperInvariant();

    private bool WhenEmpty(bool hasDevices) =>
        !hasDevices;
}
