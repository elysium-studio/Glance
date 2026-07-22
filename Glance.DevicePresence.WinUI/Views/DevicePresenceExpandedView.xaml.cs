using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;

namespace Glance.DevicePresence.WinUI;

public sealed partial class DevicePresenceExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<DevicePresenceModule> localizer;

    public DevicePresenceExpandedView(
        DevicePresenceViewModel viewModel,
        ModuleResourceTextLocalizer<DevicePresenceModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public DevicePresenceViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) =>
        value.ToUpperInvariant();

    private Visibility WhenEmpty(bool hasDevices) =>
        hasDevices ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasDevices) =>
        hasDevices ? Visibility.Visible : Visibility.Collapsed;

    private async void OpenSettings() =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
}
