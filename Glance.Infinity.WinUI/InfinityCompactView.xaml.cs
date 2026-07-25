using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Infinity.WinUI;

public sealed partial class InfinityCompactView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<InfinityModule> localizer;

    public InfinityCompactView(
        InfinityViewModel viewModel,
        ModuleResourceTextLocalizer<InfinityModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public InfinityViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private string ToDisplayText(bool isConnected, string pageTitle) => isConnected
        ? pageTitle
        : localizer.GetText("WaitingForInfinity");
}
