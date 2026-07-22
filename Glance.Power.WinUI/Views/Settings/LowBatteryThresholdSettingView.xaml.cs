using Microsoft.UI.Xaml.Controls;

namespace Glance.Power.WinUI;

public sealed partial class LowBatteryThresholdSettingView : UserControl
{
    public LowBatteryThresholdSettingView() => InitializeComponent();

    public LowBatteryThresholdSettingViewModel ViewModel => (LowBatteryThresholdSettingViewModel)DataContext;
}
