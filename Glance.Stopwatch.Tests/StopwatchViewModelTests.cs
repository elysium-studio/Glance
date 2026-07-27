namespace Glance.Stopwatch.Tests;

public sealed class StopwatchViewModelTests
{
    [Fact]
    public void Constructor_StartsStoppedAtZero()
    {
        StopwatchViewModel viewModel = new();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("00:00.00", viewModel.Elapsed);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Toggle_StartsAndPausesStopwatch()
    {
        StopwatchViewModel viewModel = new();

        viewModel.Toggle();

        Assert.True(viewModel.IsRunning);
        Assert.Equal("\uF8AE", viewModel.ToggleGlyph);

        viewModel.Toggle();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Refresh_AdvancesElapsedTimeWhileRunning()
    {
        StopwatchViewModel viewModel = new();
        viewModel.Toggle();

        Thread.Sleep(30);
        viewModel.Refresh();

        Assert.NotEqual("00:00.00", viewModel.Elapsed);
    }

    [Fact]
    public void PausedStopwatch_DoesNotAdvance()
    {
        StopwatchViewModel viewModel = new();
        viewModel.Toggle();
        Thread.Sleep(20);
        viewModel.Toggle();
        string pausedElapsed = viewModel.Elapsed;

        Thread.Sleep(20);
        viewModel.Refresh();

        Assert.Equal(pausedElapsed, viewModel.Elapsed);
    }

    [Fact]
    public void Reset_ClearsElapsedTimeAndStops()
    {
        StopwatchViewModel viewModel = new();
        viewModel.Toggle();
        Thread.Sleep(20);

        viewModel.Reset();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("00:00.00", viewModel.Elapsed);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void IsRunningChange_NotifiesToggleGlyph()
    {
        StopwatchViewModel viewModel = new();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Toggle();

        Assert.Contains(nameof(StopwatchViewModel.IsRunning), changedProperties);
        Assert.Contains(nameof(StopwatchViewModel.ToggleGlyph), changedProperties);
    }

    [Fact]
    public void Constructor_DoesNotRestoreSessionByDefault()
    {
        StopwatchViewModel viewModel = new(new StopwatchSettings
        {
            SessionElapsedTicks = TimeSpan.FromMinutes(3).Ticks,
            SessionWasRunning = true
        });

        Assert.False(viewModel.IsRunning);
        Assert.Equal("00:00.00", viewModel.Elapsed);
    }

    [Fact]
    public void Constructor_RestoresRunningSessionWhenEnabled()
    {
        DateTimeOffset now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        StopwatchViewModel viewModel = new(new StopwatchSettings
        {
            ResumeAutomatically = true,
            SessionElapsedTicks = TimeSpan.FromMinutes(2).Ticks,
            SessionUpdatedUtc = now - TimeSpan.FromMinutes(1),
            SessionWasRunning = true
        }, now);

        Assert.True(viewModel.IsRunning);
        Assert.StartsWith("03:00.", viewModel.Elapsed);
    }

    [Fact]
    public void WriteSessionState_PreservesResumePreference()
    {
        StopwatchSettings settings = new() { ResumeAutomatically = true };
        StopwatchViewModel viewModel = new();
        viewModel.Toggle();

        viewModel.WriteSessionState(settings, DateTimeOffset.UnixEpoch);

        Assert.True(settings.ResumeAutomatically);
        Assert.True(settings.SessionWasRunning);
        Assert.Equal(DateTimeOffset.UnixEpoch, settings.SessionUpdatedUtc);
    }
}
