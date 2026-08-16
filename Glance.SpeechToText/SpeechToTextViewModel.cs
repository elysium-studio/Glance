using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using Glance.Transcription;
using System.Collections.ObjectModel;
using System.Text;

namespace Glance.SpeechToText;

public sealed partial class SpeechToTextViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private readonly ITextLocalizer localizer = localizer;
    private readonly StringBuilder transcriptBuilder = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListening))]
    [NotifyPropertyChangedFor(nameof(IsPaused))]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(CanToggleListening))]
    [NotifyPropertyChangedFor(nameof(CanChangeAudioSource))]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(ToggleToolTip))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(MainText))]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private SpeechToTextState state = SpeechToTextState.Loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleListening))]
    [NotifyPropertyChangedFor(nameof(CanChangeAudioSource))]
    [NotifyPropertyChangedFor(nameof(AudioSourceName))]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(MainText))]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private AudioInputSource? selectedAudioSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTranscript))]
    private string transcript = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(MainText))]
    private string partialText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    [NotifyPropertyChangedFor(nameof(MainText))]
    private string latestFinalText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private string errorMessage = string.Empty;

    public ObservableCollection<AudioInputSource> AudioSources { get; } = [];

    public bool IsListening => State == SpeechToTextState.Listening;

    public bool IsPaused => State == SpeechToTextState.Paused;

    public bool IsBusy => State is SpeechToTextState.Loading or SpeechToTextState.Starting;

    public bool HasTranscript => !string.IsNullOrWhiteSpace(Transcript);

    public bool CanToggleListening => SelectedAudioSource is not null && State is (SpeechToTextState.Ready or SpeechToTextState.Listening or SpeechToTextState.Paused or SpeechToTextState.Error);

    public bool CanChangeAudioSource => !IsBusy && !IsListening && AudioSources.Count > 0;

    public string ToggleGlyph => IsListening ? "\uF8AE" : "\uF5B0";

    public string ToggleToolTip => IsListening
        ? localizer.GetText("PauseTranscription")
        : IsPaused
            ? localizer.GetText("ResumeTranscription")
            : localizer.GetText("StartTranscription");

    public string AudioSourceName => SelectedAudioSource?.DisplayName ?? localizer.GetText("NoMicrophone");

    public string CompactStatusText => State switch
    {
        SpeechToTextState.Loading => localizer.GetText("GettingReady"),
        SpeechToTextState.ModelRequired => localizer.GetText("SetupRequired"),
        SpeechToTextState.Starting => localizer.GetText("Starting"),
        SpeechToTextState.Listening => GetCurrentText(localizer.GetText("Listening")),
        SpeechToTextState.Paused => localizer.GetText("Paused"),
        SpeechToTextState.Error => localizer.GetText("Unavailable"),
        _ => SelectedAudioSource is null
            ? localizer.GetText("NoMicrophone")
            : localizer.GetText("ModuleDisplayName")
    };

    public string MainText => State switch
    {
        SpeechToTextState.Loading => localizer.GetText("GettingReady"),
        SpeechToTextState.ModelRequired => localizer.GetText("SetupRequired"),
        SpeechToTextState.Starting => localizer.GetText("Starting"),
        SpeechToTextState.Listening => GetCurrentText(localizer.GetText("Listening")),
        SpeechToTextState.Paused => GetCurrentText(localizer.GetText("TranscriptionPaused")),
        SpeechToTextState.Error => localizer.GetText("Unavailable"),
        _ => SelectedAudioSource is null
            ? localizer.GetText("NoMicrophone")
            : HasTranscript
                ? LatestFinalText
                : localizer.GetText("ReadyToTranscribe")
    };

    public string SubtitleText => State switch
    {
        SpeechToTextState.Loading => localizer.GetText("CheckingSpeechModel"),
        SpeechToTextState.ModelRequired => localizer.GetText("DownloadModelInSettings"),
        SpeechToTextState.Starting => AudioSourceName,
        SpeechToTextState.Listening => AudioSourceName,
        SpeechToTextState.Paused => AudioSourceName,
        SpeechToTextState.Error => string.IsNullOrWhiteSpace(ErrorMessage)
            ? localizer.GetText("TryAgain")
            : ErrorMessage,
        _ => SelectedAudioSource is null
            ? localizer.GetText("ConnectMicrophone")
            : localizer.GetText("SelectPlayToStart")
    };

    public event EventHandler? ToggleListeningRequested;

    public event EventHandler<AudioInputSource>? AudioSourceChanged;

    public event EventHandler? ClearRequested;

    public event EventHandler<string>? CopyRequested;

    public void ToggleListening() => ToggleListeningRequested?.Invoke(this, EventArgs.Empty);

    public void SelectAudioSource(AudioInputSource source) => SelectedAudioSource = source;

    public void Clear() => ClearRequested?.Invoke(this, EventArgs.Empty);

    public void Copy()
    {
        if (HasTranscript)
        {
            CopyRequested?.Invoke(this, Transcript);
        }
    }

    public void SetAudioSources(IEnumerable<AudioInputSource> sources)
    {
        string? selectedId = SelectedAudioSource?.Id;
        AudioSources.Clear();

        foreach (AudioInputSource source in sources)
        {
            AudioSources.Add(source);
        }

        SelectedAudioSource = AudioSources.FirstOrDefault(source => string.Equals(source.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ??
            AudioSources.FirstOrDefault(source => source.IsDefault) ??
            AudioSources.FirstOrDefault();
        NotifySourceAvailabilityChanged();
    }

    public void SetModelRequired()
    {
        ErrorMessage = string.Empty;
        State = SpeechToTextState.ModelRequired;
    }

    public void SetReady()
    {
        ErrorMessage = string.Empty;
        State = SpeechToTextState.Ready;
    }

    public void BeginStarting()
    {
        ErrorMessage = string.Empty;
        State = SpeechToTextState.Starting;
    }

    public void BeginListening()
    {
        ErrorMessage = string.Empty;
        State = SpeechToTextState.Listening;
    }

    public void PauseListening() => State = SpeechToTextState.Paused;

    public void ShowError(string message)
    {
        ErrorMessage = message;
        State = SpeechToTextState.Error;
    }

    public void ApplyRecognition(TranscriptionResult result)
    {
        string text = result.Text.Trim();

        if (!result.IsFinal)
        {
            PartialText = text;
            return;
        }

        PartialText = string.Empty;

        if (text.Length == 0)
        {
            return;
        }

        if (transcriptBuilder.Length > 0)
        {
            transcriptBuilder.Append(' ');
        }

        transcriptBuilder.Append(text);
        LatestFinalText = text;
        Transcript = transcriptBuilder.ToString();
    }

    public void ClearTranscript()
    {
        transcriptBuilder.Clear();
        Transcript = string.Empty;
        PartialText = string.Empty;
        LatestFinalText = string.Empty;
    }

    partial void OnSelectedAudioSourceChanged(AudioInputSource? oldValue,
        AudioInputSource? newValue)
    {
        if (newValue is not null && !string.Equals(oldValue?.Id, newValue.Id, StringComparison.OrdinalIgnoreCase))
        {
            AudioSourceChanged?.Invoke(this, newValue);
        }
    }

    private string GetCurrentText(string fallback) => !string.IsNullOrWhiteSpace(PartialText)
        ? PartialText
        : !string.IsNullOrWhiteSpace(LatestFinalText)
            ? LatestFinalText
            : fallback;

    private void NotifySourceAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanChangeAudioSource));
        OnPropertyChanged(nameof(CanToggleListening));
        OnPropertyChanged(nameof(CompactStatusText));
        OnPropertyChanged(nameof(MainText));
        OnPropertyChanged(nameof(SubtitleText));
    }
}
