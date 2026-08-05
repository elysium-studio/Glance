using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.QuickConvert.WinUI;

public sealed partial class QuickConvertCompactView :
    UserControl
{
    public QuickConvertCompactView(QuickConvertViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public QuickConvertViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
