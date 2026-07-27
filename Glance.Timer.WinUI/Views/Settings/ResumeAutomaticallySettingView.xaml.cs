using Microsoft.UI.Xaml.Controls;

namespace Glance.Timer.WinUI;

public sealed partial class TimerResumeAutomaticallySettingView :
    UserControl
{
    public TimerResumeAutomaticallySettingView() => InitializeComponent();

    public TimerResumeAutomaticallySettingViewModel ViewModel => (TimerResumeAutomaticallySettingViewModel)DataContext;
}
