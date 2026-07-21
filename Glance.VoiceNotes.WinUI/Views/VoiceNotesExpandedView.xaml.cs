using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class VoiceNotesExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<VoiceNotesModule> localizer;

    public VoiceNotesExpandedView(
        VoiceNotesViewModel viewModel,
        ModuleResourceTextLocalizer<VoiceNotesModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public VoiceNotesViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility ToRecordingsVisibility(bool hasRecordings) =>
        hasRecordings ? Visibility.Visible : Visibility.Collapsed;

    private Visibility ToEmptyVisibility(bool hasRecordings) =>
        hasRecordings ? Visibility.Collapsed : Visibility.Visible;

    private Visibility ToRecordingVisibility(bool isRecording) =>
        isRecording ? Visibility.Visible : Visibility.Collapsed;
}
