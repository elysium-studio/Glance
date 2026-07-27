using Microsoft.UI.Xaml.Controls;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchResumeAutomaticallySettingView :
    UserControl
{
    public StopwatchResumeAutomaticallySettingView() => InitializeComponent();

    public StopwatchResumeAutomaticallySettingViewModel ViewModel => (StopwatchResumeAutomaticallySettingViewModel)DataContext;
}
