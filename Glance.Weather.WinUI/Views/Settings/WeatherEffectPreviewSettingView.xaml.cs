using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherEffectPreviewSettingView :
    UserControl
{
    public WeatherEffectPreviewSettingView() => InitializeComponent();

    public WeatherEffectPreviewSettingViewModel ViewModel => (WeatherEffectPreviewSettingViewModel)DataContext;
}
