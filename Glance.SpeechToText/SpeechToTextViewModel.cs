using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Text;

namespace Glance.SpeechToText;

public sealed partial class SpeechToTextViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private readonly ITextLocalizer localizer = localizer;
    private readonly StringBuilder transcriptBuilder = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleListening))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private SpeechRecognitionAvailability availability = SpeechRecognitionAvailability.Checking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleListening))]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private bool isListening;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleListening))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioSourceLabel))]
    private SpeechAudioSource selectedAudioSource = SpeechAudioSource.Meeting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTranscript))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private string transcript = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(ExpandedStatusText))]
    private string partialText = string.Empty;

    public bool CanToggleListening => !IsBusy && Availability == SpeechRecognitionAvailability.Ready;

    public bool HasTranscript => !string.IsNullOrWhiteSpace(Transcript);

    public string ToggleGlyph => IsListening ? "\uE71A" : "\uE720";

    public string DisplayText => !string.IsNullOrWhiteSpace(PartialText)
        ? PartialText
        : Transcript;

    public string CompactStatusText => GetStatusText(true);

    public string ExpandedStatusText => GetStatusText(false);

    public string AudioSourceLabel => SelectedAudioSource switch
    {
        SpeechAudioSource.Microphone => localizer.GetText("MicrophoneSource"),
        SpeechAudioSource.SystemAudio => localizer.GetText("SystemAudioSource"),
        _ => localizer.GetText("MeetingSource")
    };

    public event EventHandler? ToggleListeningRequested;

    public event EventHandler? EnsureModelRequested;

    public event EventHandler? ClearRequested;

    public event EventHandler<string>? CopyRequested;

    public void ToggleListening() =>
        ToggleListeningRequested?.Invoke(this, EventArgs.Empty);

    public void EnsureModel() =>
        EnsureModelRequested?.Invoke(this, EventArgs.Empty);

    public void Clear() =>
        ClearRequested?.Invoke(this, EventArgs.Empty);

    public void SelectMicrophone() =>
        SelectedAudioSource = SpeechAudioSource.Microphone;

    public void SelectSystemAudio() =>
        SelectedAudioSource = SpeechAudioSource.SystemAudio;

    public void SelectMeeting() =>
        SelectedAudioSource = SpeechAudioSource.Meeting;

    public void Copy()
    {
        if (HasTranscript)
        {
            CopyRequested?.Invoke(this, Transcript);
        }
    }

    public void SetAvailability(SpeechRecognitionAvailability availability)
    {
        Availability = availability;
        IsBusy = false;
    }

    public void BeginPreparing() =>
        IsBusy = true;

    public void BeginListening()
    {
        IsBusy = false;
        IsListening = true;
        PartialText = string.Empty;
    }

    public void StopListening()
    {
        IsBusy = false;
        IsListening = false;
        PartialText = string.Empty;
    }

    public void ApplyRecognition(string text, bool isFinal)
    {
        string normalizedText = text.Trim();

        if (!isFinal)
        {
            PartialText = normalizedText;
            return;
        }

        PartialText = string.Empty;

        if (normalizedText.Length == 0)
        {
            return;
        }

        if (transcriptBuilder.Length > 0)
        {
            transcriptBuilder.Append(' ');
        }

        transcriptBuilder.Append(normalizedText);
        Transcript = transcriptBuilder.ToString();
    }

    public void ClearTranscript()
    {
        transcriptBuilder.Clear();
        Transcript = string.Empty;
        PartialText = string.Empty;
    }

    public void ShowUnavailable()
    {
        IsBusy = false;
        IsListening = false;
        PartialText = string.Empty;
        Availability = SpeechRecognitionAvailability.Unavailable;
    }

    private string GetStatusText(bool compact)
    {
        if (IsListening)
        {
            if (!string.IsNullOrWhiteSpace(PartialText))
            {
                return PartialText;
            }

            if (compact && HasTranscript)
            {
                return Transcript;
            }

            return localizer.GetText("ListeningStatus");
        }

        if (IsBusy)
        {
            return localizer.GetText("CheckingStatus");
        }

        return Availability switch
        {
            SpeechRecognitionAvailability.Checking => localizer.GetText("CheckingStatus"),
            SpeechRecognitionAvailability.ModelRequired => localizer.GetText("ModelRequiredStatus"),
            SpeechRecognitionAvailability.PackageIdentityRequired => localizer.GetText("PackagedBuildRequiredStatus"),
            SpeechRecognitionAvailability.Unsupported => localizer.GetText("UnsupportedStatus"),
            SpeechRecognitionAvailability.Unavailable => localizer.GetText("UnavailableStatus"),
            _ when HasTranscript => Transcript,
            _ => localizer.GetText("ReadyStatus")
        };
    }
}
