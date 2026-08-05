using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class IncludeCursorSettingView :
    UserControl
{
    public IncludeCursorSettingView() => InitializeComponent();

    public IncludeCursorSettingViewModel ViewModel => (IncludeCursorSettingViewModel)DataContext;
}
