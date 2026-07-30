using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherUnitsSettingView :
    UserControl
{
    public WeatherUnitsSettingView() => InitializeComponent();

    public WeatherUnitsSettingViewModel ViewModel => (WeatherUnitsSettingViewModel)DataContext;
}
