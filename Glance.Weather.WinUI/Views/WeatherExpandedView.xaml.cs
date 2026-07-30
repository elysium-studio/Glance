using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Globalization;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherExpandedView :
    UserControl
{
    private readonly Action refresh;

    public WeatherExpandedView(WeatherViewModel viewModel, Action refresh)
    {
        ViewModel = viewModel;
        this.refresh = refresh;
        InitializeComponent();
    }

    public WeatherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public void Refresh() => refresh();

    public string ToUpper(string value) => value.ToUpper(CultureInfo.CurrentCulture);

    public Visibility WhenHasWeather(bool hasWeather) => hasWeather ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenMissingWeather(bool hasWeather) => hasWeather ? Visibility.Collapsed : Visibility.Visible;
}
