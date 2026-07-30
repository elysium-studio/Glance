using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherCompactView :
    UserControl
{
    public WeatherCompactView(WeatherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public WeatherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public Brush ToBrush(string color) => (Brush)XamlBindingHelper.ConvertValue(typeof(Brush), color);

    public Geometry ToGeometry(string data) => (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data);
}
