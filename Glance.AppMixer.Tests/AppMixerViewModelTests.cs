using Glance.Application.Abstractions;

namespace Glance.AppMixer.Tests;

public sealed class AppMixerViewModelTests
{
    [Fact]
    public void Constructor_PrefersForegroundApplication()
    {
        FakeAudioApplicationService service = new(
            new AudioApplicationSession("spotify", "Spotify", 50, false, 0.8, false, true),
            new AudioApplicationSession("msedge", "Microsoft Edge", 35, false, 0.2, true, true));

        AppMixerViewModel viewModel = new(service, new FakeLocalizer());

        Assert.Equal("msedge", viewModel.SelectedApplication?.Id);
        Assert.Equal("Microsoft Edge", viewModel.CurrentApplicationName);
        Assert.Equal(2, viewModel.Applications.Count);
    }

    [Fact]
    public void Constructor_FallsBackToLoudestApplication()
    {
        FakeAudioApplicationService service = new(
            new AudioApplicationSession("quiet", "Quiet", 50, false, 0.1, false, true),
            new AudioApplicationSession("loud", "Loud", 60, false, 0.7, false, true));

        AppMixerViewModel viewModel = new(service, new FakeLocalizer());

        Assert.Equal("loud", viewModel.SelectedApplication?.Id);
    }

    [Fact]
    public void SelectedApplication_UpdatesVolumeAndMute()
    {
        FakeAudioApplicationService service = new(new AudioApplicationSession("spotify", "Spotify", 50, false, 0.4, true, true));
        AppMixerViewModel viewModel = new(service, new FakeLocalizer());

        viewModel.SelectedApplication!.Volume = 24;
        viewModel.SelectedApplication.ToggleMute();

        Assert.Equal(("spotify", 24), service.LastVolume);
        Assert.Equal(("spotify", true), service.LastMute);
    }

    private sealed class FakeAudioApplicationService(params AudioApplicationSession[] sessions) :
        IAudioApplicationService
    {
        public (string Id, int Volume)? LastVolume { get; private set; }

        public (string Id, bool IsMuted)? LastMute { get; private set; }

        public IReadOnlyList<AudioApplicationSession> GetApplications() => sessions;

        public bool TrySetVolume(string applicationId,
            int volumePercent)
        {
            LastVolume = (applicationId, volumePercent);
            return true;
        }

        public bool TrySetMuted(string applicationId,
            bool isMuted)
        {
            LastMute = (applicationId, isMuted);
            return true;
        }
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key,
            params object[] arguments) => key switch
            {
                "NoAudioPlaying" => "No apps playing sound",
                "NoApplicationVolume" => "Play audio in an app to control it",
                _ => key
            };
    }
}
