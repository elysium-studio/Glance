using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.KeepAwake.WinUI;

public sealed partial class KeepAwakeExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<KeepAwakeModule> localizer;

    public KeepAwakeExpandedView(KeepAwakeViewModel viewModel,
        ModuleResourceTextLocalizer<KeepAwakeModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public KeepAwakeViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private bool IsActionEnabled(bool isBusy) =>
        !isBusy;

    private string ToUpper(string value) =>
        value.ToUpperInvariant();
}
