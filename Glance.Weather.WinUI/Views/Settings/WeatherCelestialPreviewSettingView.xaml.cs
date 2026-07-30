using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherCelestialPreviewSettingView :
    UserControl
{
    public WeatherCelestialPreviewSettingView() => InitializeComponent();

    public WeatherCelestialPreviewSettingViewModel ViewModel => (WeatherCelestialPreviewSettingViewModel)DataContext;
}
