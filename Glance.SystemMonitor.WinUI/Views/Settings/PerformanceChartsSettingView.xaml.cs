using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class PerformanceChartsSettingView :
    UserControl
{
    public PerformanceChartsSettingView() => InitializeComponent();

    public PerformanceChartsSettingViewModel ViewModel => (PerformanceChartsSettingViewModel)DataContext;
}
