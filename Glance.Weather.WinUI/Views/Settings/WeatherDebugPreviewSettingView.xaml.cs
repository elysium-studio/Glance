using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherDebugPreviewSettingView :
    UserControl
{
    public WeatherDebugPreviewSettingView() => InitializeComponent();

    public WeatherDebugPreviewSettingViewModel ViewModel => (WeatherDebugPreviewSettingViewModel)DataContext;

    public Visibility When(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
