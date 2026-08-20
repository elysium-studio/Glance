using Glance.Inspector;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Inspector.WinUI;

public sealed partial class InspectorExpandedView :
    UserControl
{
    public InspectorExpandedView(InspectorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public InspectorViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToUpper(string value) => value.ToUpperInvariant();
}
