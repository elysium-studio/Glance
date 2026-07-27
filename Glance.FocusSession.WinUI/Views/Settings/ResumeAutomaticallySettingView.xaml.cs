using Microsoft.UI.Xaml.Controls;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusSessionResumeAutomaticallySettingView :
    UserControl
{
    public FocusSessionResumeAutomaticallySettingView() => InitializeComponent();

    public FocusSessionResumeAutomaticallySettingViewModel ViewModel => (FocusSessionResumeAutomaticallySettingViewModel)DataContext;
}
