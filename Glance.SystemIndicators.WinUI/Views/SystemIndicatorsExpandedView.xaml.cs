using Glance.SystemIndicators;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemIndicators.WinUI;

public sealed partial class SystemIndicatorsExpandedView :
    UserControl
{
    public SystemIndicatorsExpandedView(SystemIndicatorsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SystemIndicatorsViewModel ViewModel { get; }

    public string ToUpper(string value) => value.ToUpper(System.Globalization.CultureInfo.CurrentCulture);
}
