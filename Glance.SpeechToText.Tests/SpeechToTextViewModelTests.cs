using Glance.Application.Abstractions;
using Glance.Transcription;

namespace Glance.SpeechToText.Tests;

public sealed class SpeechToTextViewModelTests
{
    [Fact]
    public void ModelRequired_ShowsSetupGuidance()
    {
        SpeechToTextViewModel viewModel = new(new FakeLocalizer());

        viewModel.SetModelRequired();

        Assert.Equal(SpeechToTextState.ModelRequired, viewModel.State);
        Assert.Equal("Set up speech recognition", viewModel.MainText);
        Assert.Equal("Download a speech model in settings", viewModel.SubtitleText);
        Assert.False(viewModel.CanToggleListening);
    }

    [Fact]
    public void AudioSources_SelectDefaultMicrophone()
    {
        SpeechToTextViewModel viewModel = new(new FakeLocalizer());
        AudioInputSource secondary = new("secondary", "USB microphone");
        AudioInputSource primary = new("primary", "Studio microphone", true);

        viewModel.SetAudioSources([secondary, primary]);
        viewModel.SetReady();

        Assert.Same(primary, viewModel.SelectedAudioSource);
        Assert.Equal("Studio microphone", viewModel.AudioSourceName);
        Assert.True(viewModel.CanToggleListening);
    }

    [Fact]
    public void ListeningAndPaused_ExposeSolidPlaybackAction()
    {
        SpeechToTextViewModel viewModel = CreateReadyViewModel();

        viewModel.BeginListening();
        Assert.True(viewModel.IsListening);
        Assert.Equal("\uF8AE", viewModel.ToggleGlyph);

        viewModel.PauseListening();
        Assert.True(viewModel.IsPaused);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Recognition_UsesPartialTextThenBuildsTranscript()
    {
        SpeechToTextViewModel viewModel = CreateReadyViewModel();
        viewModel.BeginListening();

        viewModel.ApplyRecognition(new TranscriptionResult("Hello wor", false, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        Assert.Equal("Hello wor", viewModel.MainText);

        viewModel.ApplyRecognition(new TranscriptionResult("Hello world", true, TimeSpan.Zero, TimeSpan.FromSeconds(2)));
        viewModel.ApplyRecognition(new TranscriptionResult("This is Glance", true, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));

        Assert.Equal("Hello world This is Glance", viewModel.Transcript);
        Assert.Equal("This is Glance", viewModel.MainText);
        Assert.Equal("Hello world", viewModel.SubtitleText);
        Assert.Empty(viewModel.PartialText);
    }

    [Fact]
    public void ClearAndCopy_RaiseExpectedActions()
    {
        SpeechToTextViewModel viewModel = CreateReadyViewModel();
        string? copied = null;
        int cleared = 0;
        viewModel.CopyRequested += (_, text) => copied = text;
        viewModel.ClearRequested += (_, _) => cleared++;
        viewModel.ApplyRecognition(new TranscriptionResult("Live transcript", true, TimeSpan.Zero, TimeSpan.FromSeconds(1)));

        viewModel.Copy();
        viewModel.Clear();

        Assert.Equal("Live transcript", copied);
        Assert.Equal(1, cleared);
    }

    [Fact]
    public void ClearTranscript_ReturnsToReadyStateContent()
    {
        SpeechToTextViewModel viewModel = CreateReadyViewModel();
        viewModel.ApplyRecognition(new TranscriptionResult("Text", true, TimeSpan.Zero, TimeSpan.FromSeconds(1)));

        viewModel.ClearTranscript();

        Assert.False(viewModel.HasTranscript);
        Assert.Equal("Ready to transcribe", viewModel.MainText);
    }

    private static SpeechToTextViewModel CreateReadyViewModel()
    {
        SpeechToTextViewModel viewModel = new(new FakeLocalizer());
        viewModel.SetAudioSources([new AudioInputSource("default", "Microphone", true)]);
        viewModel.SetReady();
        return viewModel;
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "SetupRequired" => "Set up speech recognition",
            "DownloadModelInSettings" => "Download a speech model in settings",
            "ReadyToTranscribe" => "Ready to transcribe",
            "Listening" => "Listening…",
            "TranscriptionPaused" => "Paused",
            "SelectPlayToStart" => "Select play to start",
            "PauseTranscription" => "Pause transcription",
            "ResumeTranscription" => "Resume transcription",
            "StartTranscription" => "Start transcription",
            _ => key
        };
    }
}
