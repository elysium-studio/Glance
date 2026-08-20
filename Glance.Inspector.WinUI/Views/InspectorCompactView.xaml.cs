using Glance.Inspector;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Inspector.WinUI;

public sealed partial class InspectorCompactView :
    UserControl
{
    public InspectorCompactView(InspectorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public InspectorViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
