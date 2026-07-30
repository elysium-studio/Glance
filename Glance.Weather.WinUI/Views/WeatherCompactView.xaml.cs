using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
}
