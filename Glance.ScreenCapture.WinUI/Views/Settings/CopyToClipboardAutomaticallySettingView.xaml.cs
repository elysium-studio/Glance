using Microsoft.UI.Xaml.Controls;

namespace Glance.ScreenCapture.WinUI;

public sealed partial class CopyToClipboardAutomaticallySettingView :
    UserControl
{
    public CopyToClipboardAutomaticallySettingView() => InitializeComponent();

    public CopyToClipboardAutomaticallySettingViewModel ViewModel => (CopyToClipboardAutomaticallySettingViewModel)DataContext;
}
