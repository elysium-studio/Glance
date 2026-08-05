using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class CountdownSecondsSettingView :
    UserControl
{
    public CountdownSecondsSettingView() => InitializeComponent();

    public CountdownSecondsSettingViewModel ViewModel => (CountdownSecondsSettingViewModel)DataContext;
}
