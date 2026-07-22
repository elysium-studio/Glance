namespace Glance.FocusSession.Tests;

public sealed class FocusSessionViewModelTests
{
    [Fact]
    public void Constructor_StartsWithFocusPhase()
    {
        FocusSessionViewModel viewModel = new();

        Assert.Equal(FocusSessionPhase.Focus, viewModel.Phase);
        Assert.Equal("25:00", viewModel.RemainingText);
        Assert.Equal(0, viewModel.Progress);
        Assert.Equal(0, viewModel.CompletedFocusSessions);
        Assert.False(viewModel.IsRunning);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void ApplySettings_UpdatesIdlePhaseDurations()
    {
        FocusSessionViewModel viewModel = new();

        viewModel.ApplySettings(new FocusSessionSettings { FocusDurationMinutes = 40, BreakDurationMinutes = 10 });
        Assert.Equal("40:00", viewModel.RemainingText);

        viewModel.Skip();
        Assert.Equal("10:00", viewModel.RemainingText);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDurations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FocusSessionViewModel(TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FocusSessionViewModel(TimeSpan.FromMinutes(25), TimeSpan.Zero));
    }

    [Fact]
    public void Toggle_StartsAndPausesSession()
    {
        FocusSessionViewModel viewModel = new();

        viewModel.Toggle();

        Assert.True(viewModel.IsRunning);
        Assert.Equal("\uF8AE", viewModel.ToggleGlyph);

        viewModel.Toggle();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("\uF5B0", viewModel.ToggleGlyph);
    }

    [Fact]
    public void Refresh_WhenStopped_DoesNothing()
    {
        FocusSessionViewModel viewModel = new();

        FocusSessionPhase? completedPhase = viewModel.Refresh();

        Assert.Null(completedPhase);
        Assert.Equal("25:00", viewModel.RemainingText);
    }

    [Fact]
    public void Refresh_AdvancesProgressWhileRunning()
    {
        FocusSessionViewModel viewModel = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        viewModel.Toggle();

        Thread.Sleep(20);
        FocusSessionPhase? completedPhase = viewModel.Refresh();

        Assert.Null(completedPhase);
        Assert.InRange(viewModel.Progress, 0.1, 100);
    }

    [Fact]
    public void CompletingFocus_AdvancesToPausedBreakAndCountsSession()
    {
        FocusSessionViewModel viewModel = new(TimeSpan.FromMilliseconds(10), TimeSpan.FromMinutes(5));
        viewModel.Toggle();

        Thread.Sleep(25);
        FocusSessionPhase? completedPhase = viewModel.Refresh();

        Assert.Equal(FocusSessionPhase.Focus, completedPhase);
        Assert.Equal(FocusSessionPhase.Break, viewModel.Phase);
        Assert.Equal("05:00", viewModel.RemainingText);
        Assert.Equal(1, viewModel.CompletedFocusSessions);
        Assert.False(viewModel.IsRunning);
        Assert.Equal(0, viewModel.Progress);
    }

    [Fact]
    public void CompletingBreak_ReturnsToFocusWithoutCountingSession()
    {
        FocusSessionViewModel viewModel = new(TimeSpan.FromMinutes(25), TimeSpan.FromMilliseconds(10));
        viewModel.Skip();
        viewModel.Toggle();

        Thread.Sleep(25);
        FocusSessionPhase? completedPhase = viewModel.Refresh();

        Assert.Equal(FocusSessionPhase.Break, completedPhase);
        Assert.Equal(FocusSessionPhase.Focus, viewModel.Phase);
        Assert.Equal("25:00", viewModel.RemainingText);
        Assert.Equal(0, viewModel.CompletedFocusSessions);
    }

    [Fact]
    public void Skip_ChangesPhaseWithoutCountingCompletion()
    {
        FocusSessionViewModel viewModel = new();

        viewModel.Skip();

        Assert.Equal(FocusSessionPhase.Break, viewModel.Phase);
        Assert.Equal("05:00", viewModel.RemainingText);
        Assert.Equal(0, viewModel.CompletedFocusSessions);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public void Reset_RestoresCurrentPhaseAndStops()
    {
        FocusSessionViewModel viewModel = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        viewModel.Toggle();
        Thread.Sleep(20);
        viewModel.Refresh();

        viewModel.Reset();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("00:01", viewModel.RemainingText);
        Assert.Equal(0, viewModel.Progress);
    }

    [Fact]
    public void StateChanges_RaiseBindablePropertyNotifications()
    {
        FocusSessionViewModel viewModel = new();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Toggle();
        viewModel.Skip();

        Assert.Contains(nameof(FocusSessionViewModel.IsRunning), changedProperties);
        Assert.Contains(nameof(FocusSessionViewModel.ToggleGlyph), changedProperties);
        Assert.Contains(nameof(FocusSessionViewModel.Phase), changedProperties);
        Assert.Contains(nameof(FocusSessionViewModel.RemainingText), changedProperties);
    }
}
