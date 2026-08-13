using Glance.SystemIndicators;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SystemIndicators.WinUI;

public sealed partial class SystemIndicatorsCompactView :
    UserControl
{
    public SystemIndicatorsCompactView(SystemIndicatorsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SystemIndicatorsViewModel ViewModel { get; }
}
