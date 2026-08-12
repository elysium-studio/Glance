using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Torrents.WinUI;

public sealed partial class TorrentExpandedView : UserControl
{
    private readonly ModuleResourceTextLocalizer<TorrentModule> localizer;

    public TorrentExpandedView(TorrentsViewModel viewModel, ModuleResourceTextLocalizer<TorrentModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public TorrentsViewModel ViewModel { get; }
    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
    public string UpperTitle => localizer.GetText("ModuleDisplayName").ToUpperInvariant();
    public string ReadyLabel => localizer.GetText("ReadyToDownload");
    public string DropInstructionLabel => localizer.GetText("DropInstruction");
    public string PauseAllLabel => localizer.GetText("PauseAll");
    public string ResumeAllLabel => localizer.GetText("ResumeAll");
    public string RemoveLabel => localizer.GetText("RemoveListOnly");
    public event EventHandler? PauseAllRequested;
    public event EventHandler? ResumeAllRequested;
    public event EventHandler<string>? RemoveRequested;

    private Visibility WhenEmpty(bool hasTorrents) => hasTorrents ? Visibility.Collapsed : Visibility.Visible;
    private Visibility WhenPopulated(bool hasTorrents) => hasTorrents ? Visibility.Visible : Visibility.Collapsed;
    private Visibility PauseVisibility(bool canPause, bool canResume) => canPause || !canResume ? Visibility.Visible : Visibility.Collapsed;
    private Visibility ResumeVisibility(bool canPause, bool canResume) => !canPause && canResume ? Visibility.Visible : Visibility.Collapsed;
    private void PauseAllClicked(object sender, RoutedEventArgs args) => PauseAllRequested?.Invoke(this, EventArgs.Empty);
    private void ResumeAllClicked(object sender, RoutedEventArgs args) => ResumeAllRequested?.Invoke(this, EventArgs.Empty);
    private void RemoveClicked(object sender, RoutedEventArgs args) => RemoveRequested?.Invoke(this, ((Button)sender).Tag?.ToString() ?? string.Empty);
}
