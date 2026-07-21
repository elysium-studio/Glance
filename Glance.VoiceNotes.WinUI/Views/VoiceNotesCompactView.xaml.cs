using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class VoiceNotesCompactView :
    UserControl
{
    public VoiceNotesCompactView(VoiceNotesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public VoiceNotesViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private Visibility ToRecordingVisibility(bool isRecording) =>
        isRecording ? Visibility.Visible : Visibility.Collapsed;
}
