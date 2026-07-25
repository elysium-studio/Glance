using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Infinity.WinUI;

public sealed partial class InfinityExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<InfinityModule> localizer;

    public InfinityExpandedView(
        InfinityViewModel viewModel,
        ModuleResourceTextLocalizer<InfinityModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public InfinityViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToDisplayText(bool isConnected, string pageTitle) => isConnected
        ? pageTitle
        : localizer.GetText("WaitingForInfinity");

    private string ToPageText(int pageNumber) => localizer.GetText("PageNumber", pageNumber);

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
