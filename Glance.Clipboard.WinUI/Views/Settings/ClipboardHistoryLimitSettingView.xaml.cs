using Microsoft.UI.Xaml.Controls;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardHistoryLimitSettingView : UserControl
{
    public ClipboardHistoryLimitSettingView() => InitializeComponent();

    public ClipboardHistoryLimitSettingViewModel ViewModel => (ClipboardHistoryLimitSettingViewModel)DataContext;
}
