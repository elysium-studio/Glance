using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Globalization;
using Windows.UI;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherExpandedView :
    UserControl
{
    private readonly SolidColorBrush primaryForegroundBrush = new(Color.FromArgb(255, 248, 250, 252));
    private readonly SolidColorBrush secondaryForegroundBrush = new(Color.FromArgb(204, 248, 250, 252));

    public WeatherExpandedView(WeatherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public WeatherViewModel ViewModel { get; }

    public Brush PrimaryForegroundBrush => primaryForegroundBrush;

    public Brush SecondaryForegroundBrush => secondaryForegroundBrush;

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public Brush ToBrush(string color) => (Brush)XamlBindingHelper.ConvertValue(typeof(Brush), color);

    public Geometry ToGeometry(string data) => (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);

    public string ToUpper(string value) => value.ToUpper(CultureInfo.CurrentCulture);

    public Visibility WhenHasWeather(bool hasWeather) => hasWeather ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenMissingWeather(bool hasWeather) => hasWeather ? Visibility.Collapsed : Visibility.Visible;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ActualThemeChanged -= HandleActualThemeChanged;
        ActualThemeChanged += HandleActualThemeChanged;
        UpdateForeground();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ActualThemeChanged -= HandleActualThemeChanged;
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (WeatherForegroundProperties.Contains(args.PropertyName))
        {
            UpdateForeground();
        }
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => UpdateForeground();

    private void UpdateForeground()
    {
        (Color primary, Color secondary) = WeatherScenePalette.GetForegroundColors(ViewModel, ActualTheme);
        primaryForegroundBrush.Color = primary;
        secondaryForegroundBrush.Color = secondary;
    }
}
