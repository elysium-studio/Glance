using Microsoft.UI.Xaml.Controls;

namespace Glance.DevicePresence.WinUI;

public sealed partial class DevicePresenceLowBatteryThresholdSettingView : UserControl
{
    public DevicePresenceLowBatteryThresholdSettingView() => InitializeComponent();

    public DevicePresenceLowBatteryThresholdSettingViewModel ViewModel => (DevicePresenceLowBatteryThresholdSettingViewModel)DataContext;
}
