using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherExpandedView :
    UserControl
{
    public WeatherExpandedView(WeatherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public WeatherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string ToUpper(string value) => value.ToUpper(CultureInfo.CurrentCulture);

    public Visibility WhenHasWeather(bool hasWeather) => hasWeather ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenMissingWeather(bool hasWeather) => hasWeather ? Visibility.Collapsed : Visibility.Visible;
}
