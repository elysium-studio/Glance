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
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = localizer.GetText("ModelConsentTitle"),
            Content = localizer.GetText("ModelConsentMessage"),
            PrimaryButtonText = localizer.GetText("ModelConsentDownload"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.EnsureModel();
        }
    }

    private string ToUpper(string value) =>
        value.ToUpperInvariant();

    private Visibility ToTranscriptVisibility(bool hasTranscript) =>
        hasTranscript ? Visibility.Visible : Visibility.Collapsed;

    private Visibility ToModelRequiredVisibility(SpeechRecognitionAvailability availability) =>
        availability == SpeechRecognitionAvailability.ModelRequired
            ? Visibility.Visible
            : Visibility.Collapsed;

    private Visibility ToReadyVisibility(SpeechRecognitionAvailability availability) =>
        availability == SpeechRecognitionAvailability.Ready
            ? Visibility.Visible
            : Visibility.Collapsed;

    private bool CanChangeAudioSource(bool isListening,
        bool isBusy) =>
        !isListening && !isBusy;

    private bool IsMeetingSource(SpeechAudioSource source) =>
        source == SpeechAudioSource.Meeting;

    private bool IsMicrophoneSource(SpeechAudioSource source) =>
        source == SpeechAudioSource.Microphone;

    private bool IsSystemAudioSource(SpeechAudioSource source) =>
        source == SpeechAudioSource.SystemAudio;
}
