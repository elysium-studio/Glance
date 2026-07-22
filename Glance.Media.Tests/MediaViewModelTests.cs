using Glance.Application.Abstractions;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;

namespace Glance.Media.Tests;

public sealed class MediaViewModelTests
{
    [Fact]
    public void Constructor_UsesLocalizedEmptyState()
    {
        MediaViewModel viewModel = new(new TestTextLocalizer());

        Assert.Equal("NothingPlaying", viewModel.Title);
        Assert.Equal("OpenMediaApp", viewModel.Artist);
        Assert.Equal("ModuleTitle", viewModel.Source);
        Assert.False(viewModel.HasSession);
        Assert.False(viewModel.CanTogglePlayback);
        Assert.Null(viewModel.Artwork);
    }

    [Theory]
    [InlineData(false, "\uF5B0")]
    [InlineData(true, "\uF8AE")]
    public void PlayPauseGlyph_ReflectsPlaybackState(bool isPlaying, string expected)
    {
        MediaViewModel viewModel = new(new TestTextLocalizer())
        {
            IsPlaying = isPlaying
        };

        Assert.Equal(expected, viewModel.PlayPauseGlyph);
    }

    [Theory]
    [InlineData(MediaPlaybackAction.Previous)]
    [InlineData(MediaPlaybackAction.TogglePlayback)]
    [InlineData(MediaPlaybackAction.Next)]
    public void PlaybackMethods_RaiseRequestedAction(MediaPlaybackAction expected)
    {
        MediaViewModel viewModel = new(new TestTextLocalizer());
        MediaPlaybackAction? actual = null;
        viewModel.PlaybackActionRequested += (_, action) => actual = action;

        switch (expected)
        {
            case MediaPlaybackAction.Previous:
                viewModel.Previous();
                break;
            case MediaPlaybackAction.TogglePlayback:
                viewModel.TogglePlayback();
                break;
            case MediaPlaybackAction.Next:
                viewModel.Next();
                break;
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UpdateAudioLevels_CopiesValuesIntoEventArgs()
    {
        MediaViewModel viewModel = new(new TestTextLocalizer());
        IReadOnlyList<double>? actual = null;
        viewModel.AudioLevelsChanged += (_, args) => actual = args.Levels;
        double[] source = [0.1, 0.5, 0.9];

        viewModel.UpdateAudioLevels(source);
        source[0] = 1;

        Assert.Equal([0.1, 0.5, 0.9], actual);
    }

    [Fact]
    public void CapabilityProperties_CanBeUpdatedTogether()
    {
        MediaViewModel viewModel = new(new TestTextLocalizer())
        {
            HasSession = true,
            CanSkipPrevious = true,
            CanSkipNext = true,
            CanTogglePlayback = true
        };

        Assert.True(viewModel.HasSession);
        Assert.True(viewModel.CanSkipPrevious);
        Assert.True(viewModel.CanSkipNext);
        Assert.True(viewModel.CanTogglePlayback);
    }

    [Fact]
    public void OptionsChanged_UpdatesAudioVisualizationVisibility()
    {
        WeakReferenceMessenger messenger = new();
        using MediaViewModel viewModel = new(new TestTextLocalizer(), new MediaSettings { ShowAudioVisualization = true }, messenger);

        messenger.Send(new OptionsChangedEventArgs<MediaSettings>(new MediaSettings { ShowAudioVisualization = false }));

        Assert.False(viewModel.ShowAudioVisualization);
    }

    private sealed class TestTextLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key;
    }
}
