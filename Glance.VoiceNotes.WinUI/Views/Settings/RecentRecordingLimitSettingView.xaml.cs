using Microsoft.UI.Xaml.Controls;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class RecentRecordingLimitSettingView : UserControl
{
    public RecentRecordingLimitSettingView() => InitializeComponent();

    public RecentRecordingLimitSettingViewModel ViewModel => (RecentRecordingLimitSettingViewModel)DataContext;
}
