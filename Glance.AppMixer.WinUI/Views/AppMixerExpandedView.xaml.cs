using Glance.AppMixer;
using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.AppMixer.WinUI;

public sealed partial class AppMixerExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<AppMixerModule> localizer;

    public AppMixerExpandedView(AppMixerViewModel viewModel,
        ModuleResourceTextLocalizer<AppMixerModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public AppMixerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private static string ToUpper(string value) => value.ToUpperInvariant();

    private static Visibility WhenEmpty(bool hasApplications) => hasApplications ? Visibility.Collapsed : Visibility.Visible;

    private static Visibility WhenPopulated(bool hasApplications) => hasApplications ? Visibility.Visible : Visibility.Collapsed;
}
