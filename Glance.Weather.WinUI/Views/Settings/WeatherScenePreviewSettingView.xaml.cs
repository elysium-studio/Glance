using Microsoft.UI.Xaml.Controls;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherScenePreviewSettingView :
    UserControl
{
    public WeatherScenePreviewSettingView() => InitializeComponent();

    public WeatherScenePreviewSettingViewModel ViewModel => (WeatherScenePreviewSettingViewModel)DataContext;
}
