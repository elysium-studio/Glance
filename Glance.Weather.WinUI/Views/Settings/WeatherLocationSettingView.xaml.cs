using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherLocationSettingView :
    UserControl
{
    public WeatherLocationSettingView() => InitializeComponent();

    public WeatherLocationSettingViewModel ViewModel => (WeatherLocationSettingViewModel)DataContext;
}
