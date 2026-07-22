using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerAdjustmentSettingView :
    UserControl
{
    public TimerAdjustmentSettingView() => InitializeComponent();

    public TimerAdjustmentSettingViewModel ViewModel => (TimerAdjustmentSettingViewModel)DataContext;
}
