using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherTemperaturePreviewSettingView :
    UserControl
{
    public WeatherTemperaturePreviewSettingView() => InitializeComponent();

    public WeatherTemperaturePreviewSettingViewModel ViewModel => (WeatherTemperaturePreviewSettingViewModel)DataContext;
}
