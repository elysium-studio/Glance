using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class ScreenRecorderRecentRecordingLimitSettingView :
    UserControl
{
    public ScreenRecorderRecentRecordingLimitSettingView() => InitializeComponent();

    public ScreenRecorderRecentRecordingLimitSettingViewModel ViewModel => (ScreenRecorderRecentRecordingLimitSettingViewModel)DataContext;
}
