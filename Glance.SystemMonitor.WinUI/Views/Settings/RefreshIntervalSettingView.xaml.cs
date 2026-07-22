using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class RefreshIntervalSettingView : UserControl
{
    public RefreshIntervalSettingView() => InitializeComponent();

    public RefreshIntervalSettingViewModel ViewModel => (RefreshIntervalSettingViewModel)DataContext;
}
