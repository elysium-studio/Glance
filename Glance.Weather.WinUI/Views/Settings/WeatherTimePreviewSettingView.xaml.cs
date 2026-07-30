using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherTimePreviewSettingView :
    UserControl
{
    public WeatherTimePreviewSettingView() => InitializeComponent();

    public WeatherTimePreviewSettingViewModel ViewModel => (WeatherTimePreviewSettingViewModel)DataContext;
}
