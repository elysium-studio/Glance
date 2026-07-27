using Microsoft.UI.Xaml.Controls;

namespace Glance.KeepAwake.WinUI;

public sealed partial class KeepAwakeResumeAutomaticallySettingView :
    UserControl
{
    public KeepAwakeResumeAutomaticallySettingView() => InitializeComponent();

    public KeepAwakeResumeAutomaticallySettingViewModel ViewModel => (KeepAwakeResumeAutomaticallySettingViewModel)DataContext;
}
