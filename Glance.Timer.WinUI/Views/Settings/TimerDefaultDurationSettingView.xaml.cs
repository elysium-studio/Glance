using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerDefaultDurationSettingView :
    UserControl
{
    public TimerDefaultDurationSettingView() => InitializeComponent();

    public TimerDefaultDurationSettingViewModel ViewModel => (TimerDefaultDurationSettingViewModel)DataContext;
}
