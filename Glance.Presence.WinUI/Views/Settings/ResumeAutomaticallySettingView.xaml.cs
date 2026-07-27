using Microsoft.UI.Xaml.Controls;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceResumeAutomaticallySettingView :
    UserControl
{
    public PresenceResumeAutomaticallySettingView() => InitializeComponent();

    public PresenceResumeAutomaticallySettingViewModel ViewModel => (PresenceResumeAutomaticallySettingViewModel)DataContext;
}
