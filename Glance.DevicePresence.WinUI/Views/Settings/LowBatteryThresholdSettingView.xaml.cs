using Microsoft.UI.Xaml.Controls;

namespace Glance.DevicePresence.WinUI;

public sealed partial class LowBatteryThresholdSettingView : UserControl
{
    public LowBatteryThresholdSettingView() => InitializeComponent();

    public LowBatteryThresholdSettingViewModel ViewModel => (LowBatteryThresholdSettingViewModel)DataContext;
}
