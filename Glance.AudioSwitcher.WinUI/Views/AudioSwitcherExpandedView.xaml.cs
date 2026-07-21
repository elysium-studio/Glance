using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.AudioSwitcher.WinUI;

public sealed partial class AudioSwitcherExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<AudioSwitcherModule> localizer;

    public AudioSwitcherExpandedView(
        AudioSwitcherViewModel viewModel,
        ModuleResourceTextLocalizer<AudioSwitcherModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public AudioSwitcherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();
}
