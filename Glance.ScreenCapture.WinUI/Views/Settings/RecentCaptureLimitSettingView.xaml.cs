using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class RecentCaptureLimitSettingView : UserControl
{
    public RecentCaptureLimitSettingView() => InitializeComponent();

    public RecentCaptureLimitSettingViewModel ViewModel => (RecentCaptureLimitSettingViewModel)DataContext;
}
