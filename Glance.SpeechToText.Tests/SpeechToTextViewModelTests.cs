namespace Glance.SpeechToText.Tests;

public sealed class SpeechToTextViewModelTests
{
    [Fact]
    public void PartialRecognition_UpdatesLiveTextWithoutCommittingTranscript()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();
        viewModel.BeginListening();

        viewModel.ApplyRecognition("hello wor", false);

        Assert.Equal("hello wor", viewModel.PartialText);
        Assert.Equal("hello wor", viewModel.CompactStatusText);
        Assert.Empty(viewModel.Transcript);
    }

    [Fact]
    public void FinalRecognition_AccumulatesConfirmedPhrases()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();

        viewModel.ApplyRecognition("Hello there.", true);
        viewModel.ApplyRecognition("Welcome to the meeting.", true);

        Assert.Equal("Hello there. Welcome to the meeting.", viewModel.Transcript);
        Assert.True(viewModel.HasTranscript);
    }

    [Fact]
    public void FinalRecognition_ReplacesPartialPhrase()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();
        viewModel.ApplyRecognition("testing one", false);

        viewModel.ApplyRecognition("Testing one two.", true);

        Assert.Empty(viewModel.PartialText);
        Assert.Equal("Testing one two.", viewModel.DisplayText);
    }

    [Fact]
    public void ClearTranscript_RemovesPartialAndConfirmedText()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();
        viewModel.ApplyRecognition("Confirmed.", true);
        viewModel.ApplyRecognition("partial", false);

        viewModel.ClearTranscript();

        Assert.Empty(viewModel.Transcript);
        Assert.Empty(viewModel.PartialText);
        Assert.False(viewModel.HasTranscript);
    }

    [Fact]
    public void AudioSourceFunctions_SelectExpectedLiveSource()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();

        viewModel.SelectMicrophone();
        Assert.Equal(SpeechAudioSource.Microphone, viewModel.SelectedAudioSource);

        viewModel.SelectSystemAudio();
        Assert.Equal(SpeechAudioSource.SystemAudio, viewModel.SelectedAudioSource);

        viewModel.SelectMeeting();
        Assert.Equal(SpeechAudioSource.Meeting, viewModel.SelectedAudioSource);
        Assert.Equal("Microphone and system audio", viewModel.AudioSourceLabel);
    }

    [Fact]
    public void ReadyAvailability_EnablesListening()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();

        viewModel.SetAvailability(SpeechRecognitionAvailability.Ready);

        Assert.True(viewModel.CanToggleListening);
        Assert.Equal("Ready to transcribe", viewModel.ExpandedStatusText);
    }

    [Fact]
    public void ToggleListening_RaisesFunctionEvent()
    {
        SpeechToTextViewModel viewModel = CreateViewModel();
        int requests = 0;
        viewModel.ToggleListeningRequested += (_, _) => requests++;

        viewModel.ToggleListening();

        Assert.Equal(1, requests);
    }

    private static SpeechToTextViewModel CreateViewModel() => new(new FakeLocalizer());

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "ReadyStatus" => "Ready to transcribe",
            "ListeningStatus" => "Listening…",
            "CheckingStatus" => "Checking speech support…",
            "ModelRequiredStatus" => "Speech model required",
            "PackagedBuildRequiredStatus" => "Available in the packaged app",
            "UnsupportedStatus" => "Speech recognition is not supported",
            "UnavailableStatus" => "Speech recognition unavailable",
            "MicrophoneSource" => "Microphone",
            "SystemAudioSource" => "System audio",
            "MeetingSource" => "Microphone and system audio",
            _ => key
        };
    }
}
