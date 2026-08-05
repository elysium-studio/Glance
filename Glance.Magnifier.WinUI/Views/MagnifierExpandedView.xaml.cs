using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Magnifier.WinUI;

public sealed partial class MagnifierExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<MagnifierModule> localizer;

    public MagnifierExpandedView(MagnifierViewModel viewModel,
        ModuleResourceTextLocalizer<MagnifierModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public MagnifierViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    public string StartLabel => localizer.GetText("StartLabel");

    public string ZoomInLabel => localizer.GetText("ZoomInLabel");

    public string ZoomOutLabel => localizer.GetText("ZoomOutLabel");

    public string CloseLabel => localizer.GetText("CloseLabel");

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenRunning(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private Visibility WhenStopped(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
}
