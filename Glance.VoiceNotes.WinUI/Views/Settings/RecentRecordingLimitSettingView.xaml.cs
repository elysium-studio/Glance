using Microsoft.UI.Xaml.Controls;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class VoiceNotesRecentRecordingLimitSettingView :
    UserControl
{
    public VoiceNotesRecentRecordingLimitSettingView() => InitializeComponent();

    public VoiceNotesRecentRecordingLimitSettingViewModel ViewModel => (VoiceNotesRecentRecordingLimitSettingViewModel)DataContext;
}
