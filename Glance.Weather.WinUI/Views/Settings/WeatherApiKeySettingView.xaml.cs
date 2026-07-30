using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherApiKeySettingView :
    UserControl
{
    public WeatherApiKeySettingView() => InitializeComponent();

    public WeatherApiKeySettingViewModel ViewModel => (WeatherApiKeySettingViewModel)DataContext;
}
