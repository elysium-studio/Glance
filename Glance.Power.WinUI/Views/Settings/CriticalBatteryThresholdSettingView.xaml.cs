using Microsoft.UI.Xaml.Controls;

namespace Glance.Power.WinUI;

public sealed partial class CriticalBatteryThresholdSettingView : UserControl
{
    public CriticalBatteryThresholdSettingView() => InitializeComponent();

    public CriticalBatteryThresholdSettingViewModel ViewModel => (CriticalBatteryThresholdSettingViewModel)DataContext;
}
