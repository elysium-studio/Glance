using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Fasting.WinUI;

public sealed partial class FastingExpandedView :
    UserControl
{
    private readonly CompositionActivityPulse activityPulse;

    public FastingExpandedView(FastingViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        activityPulse = new(this, PulseRing, viewModel, nameof(FastingViewModel.IsFasting), () => viewModel.IsFasting);
    }

    public FastingViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private bool IsResetEnabled(FastingSessionStatus status) => status != FastingSessionStatus.Ready;

    private string ToUpper(string value) => value.ToUpperInvariant();
}
