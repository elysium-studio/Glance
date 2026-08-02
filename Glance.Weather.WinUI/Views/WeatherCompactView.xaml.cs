using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using Windows.UI;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherCompactView :
    UserControl
{
    private readonly SolidColorBrush primaryForegroundBrush = new(Color.FromArgb(255, 248, 250, 252));

    public WeatherCompactView(WeatherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public WeatherViewModel ViewModel { get; }

    public Brush PrimaryForegroundBrush => primaryForegroundBrush;

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public Brush ToBrush(string color) => (Brush)XamlBindingHelper.ConvertValue(typeof(Brush), color);

    public Geometry ToGeometry(string data) => (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);

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

    private void UpdateForeground() => primaryForegroundBrush.Color = WeatherScenePalette.GetForegroundColors(ViewModel, ActualTheme).Primary;
}
