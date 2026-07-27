namespace Glance.Timer.Tests;

public sealed class TimerViewModelTests
{
    [Fact]
    public void Constructor_StartsWithFiveMinutes()
    {
        TimerViewModel viewModel = new();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("05:00", viewModel.RemainingText);
        Assert.True(viewModel.CanDecreaseMinute);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Constructor_UsesModuleSettings()
    {
        TimerViewModel viewModel = new(new TimerSettings { DefaultDurationMinutes = 12, AdjustmentMinutes = 2 });

        viewModel.AddMinute();

        Assert.Equal("14:00", viewModel.RemainingText);
    }

    [Fact]
    public void ApplySettings_UpdatesAnIdleTimer()
    {
        TimerViewModel viewModel = new();

        viewModel.ApplySettings(new TimerSettings { DefaultDurationMinutes = 20, AdjustmentMinutes = 5 });
        viewModel.DecreaseMinute();

        Assert.Equal("15:00", viewModel.RemainingText);
    }

    [Fact]
    public void AddMinute_IncreasesDurationAndRemainingTime()
    {
        TimerViewModel viewModel = new();

        viewModel.AddMinute();

        Assert.Equal("06:00", viewModel.RemainingText);
        Assert.True(viewModel.CanDecreaseMinute);
    }

    [Fact]
    public void DecreaseMinute_DecreasesDurationAndRemainingTime()
    {
        TimerViewModel viewModel = new();

        viewModel.DecreaseMinute();

        Assert.Equal("04:00", viewModel.RemainingText);
    }

    [Fact]
    public void DecreaseMinute_DoesNotGoBelowOneMinute()
    {
        TimerViewModel viewModel = new();

        for (int index = 0; index < 10; index++)
        {
            viewModel.DecreaseMinute();
        }

        Assert.Equal("01:00", viewModel.RemainingText);
        Assert.False(viewModel.CanDecreaseMinute);
    }

    [Fact]
    public void AddMinute_ReenablesDecreaseAtMinimum()
    {
        TimerViewModel viewModel = new();
        for (int index = 0; index < 4; index++)
        {
            viewModel.DecreaseMinute();
        }

        viewModel.AddMinute();

        Assert.Equal("02:00", viewModel.RemainingText);
        Assert.True(viewModel.CanDecreaseMinute);
    }

    [Fact]
    public void Toggle_StartsAndPausesTimer()
    {
        TimerViewModel viewModel = new();

        viewModel.Toggle();

        Assert.True(viewModel.IsRunning);
        Assert.Equal("\uF8AE", viewModel.ToggleGlyph);

        viewModel.Toggle();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Refresh_ReturnsFalseAndLeavesTextWhenStopped()
    {
        TimerViewModel viewModel = new();

        bool completed = viewModel.Refresh();

        Assert.False(completed);
        Assert.Equal("05:00", viewModel.RemainingText);
    }

    [Fact]
    public void Refresh_AdvancesRemainingTimeWhileRunning()
    {
        TimerViewModel viewModel = new();
        viewModel.Toggle();

        Thread.Sleep(20);
        bool completed = viewModel.Refresh();

        Assert.False(completed);
        Assert.Equal("04:59", viewModel.RemainingText);
    }

    [Fact]
    public void Reset_RestoresConfiguredDurationAndStops()
    {
        TimerViewModel viewModel = new();
        viewModel.AddMinute();
        viewModel.Toggle();
        Thread.Sleep(20);

        viewModel.Reset();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("06:00", viewModel.RemainingText);
    }

    [Fact]
    public void AdjustingRunningTimer_KeepsItRunning()
    {
        TimerViewModel viewModel = new();
        viewModel.Toggle();

        viewModel.AddMinute();
        viewModel.DecreaseMinute();

        Assert.True(viewModel.IsRunning);
        Assert.StartsWith("04:59", viewModel.RemainingText);
    }

    [Fact]
    public void IsRunningChange_NotifiesToggleGlyph()
    {
        TimerViewModel viewModel = new();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Toggle();

        Assert.Contains(nameof(TimerViewModel.IsRunning), changedProperties);
        Assert.Contains(nameof(TimerViewModel.ToggleGlyph), changedProperties);
    }

    [Fact]
    public void DurationChanges_NotifyCanDecreaseMinute()
    {
        TimerViewModel viewModel = new();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.AddMinute();
        viewModel.DecreaseMinute();

        Assert.Equal(2, changedProperties.Count(name => name == nameof(TimerViewModel.CanDecreaseMinute)));
    }

    [Fact]
    public void Constructor_DoesNotRestoreSessionByDefault()
    {
        TimerViewModel viewModel = new(new TimerSettings
        {
            SessionDurationTicks = TimeSpan.FromMinutes(12).Ticks,
            SessionRemainingTicks = TimeSpan.FromMinutes(8).Ticks,
            SessionWasRunning = true
        });

        Assert.False(viewModel.IsRunning);
        Assert.Equal("05:00", viewModel.RemainingText);
    }

    [Fact]
    public void Constructor_RestoresRunningSessionWhenEnabled()
    {
        DateTimeOffset now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        TimerViewModel viewModel = new(new TimerSettings
        {
            ResumeAutomatically = true,
            SessionDurationTicks = TimeSpan.FromMinutes(10).Ticks,
            SessionRemainingTicks = TimeSpan.FromMinutes(8).Ticks,
            SessionUpdatedUtc = now - TimeSpan.FromMinutes(2),
            SessionWasRunning = true
        }, now);

        Assert.True(viewModel.IsRunning);
        Assert.Equal("06:00", viewModel.RemainingText);
    }

    [Fact]
    public void WriteSessionState_PreservesResumePreference()
    {
        TimerSettings settings = new() { ResumeAutomatically = true };
        TimerViewModel viewModel = new();
        viewModel.Toggle();

        viewModel.WriteSessionState(settings, DateTimeOffset.UnixEpoch);

        Assert.True(settings.ResumeAutomatically);
        Assert.True(settings.SessionWasRunning);
        Assert.Equal(DateTimeOffset.UnixEpoch, settings.SessionUpdatedUtc);
    }
}
