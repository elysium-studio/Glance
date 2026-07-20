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
}
