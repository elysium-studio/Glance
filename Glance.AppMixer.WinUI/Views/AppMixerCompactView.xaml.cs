using Glance.AppMixer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.AppMixer.WinUI;

public sealed partial class AppMixerCompactView :
    UserControl
{
    public AppMixerCompactView(AppMixerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public AppMixerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private static Visibility WhenPopulated(bool hasApplications) => hasApplications ? Visibility.Visible : Visibility.Collapsed;
}
