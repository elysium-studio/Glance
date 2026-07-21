using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusSessionCompactView :
    UserControl
{
    public FocusSessionCompactView(FocusSessionViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public FocusSessionViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
