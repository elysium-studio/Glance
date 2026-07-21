using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.AudioSwitcher.WinUI;

public sealed partial class AudioSwitcherCompactView :
    UserControl
{
    public AudioSwitcherCompactView(AudioSwitcherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public AudioSwitcherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
