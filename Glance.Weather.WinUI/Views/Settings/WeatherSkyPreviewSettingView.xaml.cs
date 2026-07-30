using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherSkyPreviewSettingView :
    UserControl
{
    public WeatherSkyPreviewSettingView() => InitializeComponent();

    public WeatherSkyPreviewSettingViewModel ViewModel => (WeatherSkyPreviewSettingViewModel)DataContext;
}
