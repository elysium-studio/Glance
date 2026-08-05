using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SpeechToText.WinUI;

public sealed partial class SpeechToTextExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<SpeechToTextModule> localizer;

    public SpeechToTextExpandedView(SpeechToTextViewModel viewModel,
        ModuleResourceTextLocalizer<SpeechToTextModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public SpeechToTextViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private async void DownloadModel()
    {
        bool confirmed = await SpeechModelConsentWindow.ShowAsync(localizer.GetText("ModelConsentTitle"),
            localizer.GetText("ModelConsentMessage"),
            localizer.GetText("ModelConsentDownload"),
            localizer.GetText("Cancel"),
            XamlRoot.ContentIslandEnvironment.AppWindowId);

        if (confirmed)
        {
            ViewModel.EnsureModel();
        }
    }

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility ToTranscriptVisibility(bool hasTranscript) => hasTranscript ? Visibility.Visible : Visibility.Collapsed;

    private Visibility ToModelRequiredVisibility(SpeechRecognitionAvailability availability) => availability == SpeechRecognitionAvailability.ModelRequired
            ? Visibility.Visible
            : Visibility.Collapsed;

    private Visibility ToReadyVisibility(SpeechRecognitionAvailability availability) => availability == SpeechRecognitionAvailability.Ready
            ? Visibility.Visible
            : Visibility.Collapsed;

    private bool CanDownloadModel(bool isBusy) => !isBusy;

    private bool CanChangeAudioSource(bool isListening,
        bool isBusy) => !isListening && !isBusy;

    private bool IsMeetingSource(SpeechAudioSource source) => source == SpeechAudioSource.Meeting;

    private bool IsMicrophoneSource(SpeechAudioSource source) => source == SpeechAudioSource.Microphone;

    private bool IsSystemAudioSource(SpeechAudioSource source) => source == SpeechAudioSource.SystemAudio;
}
