namespace Glance.Hydration.Tests;

public sealed class HydrationViewModelTests
{
    private readonly HydrationReminderPolicy policy = new();
    private readonly TestTextLocalizer localizer = new();

    [Fact]
    public void AddAndUndoUpdateTodaysTotal()
    {
        DateTimeOffset now = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        HydrationSettings settings = new();
        HydrationViewModel viewModel = new(settings, policy, localizer, now);

        viewModel.Add(250, settings, now);
        Assert.Equal(250, viewModel.ConsumedMillilitres);
        Assert.True(viewModel.CanUndo);

        viewModel.Undo(settings, now);
        Assert.Equal(0, viewModel.ConsumedMillilitres);
        Assert.False(viewModel.CanUndo);
    }

    [Fact]
    public void RefreshResetsPreviousDay()
    {
        HydrationSettings settings = new()
        {
            TrackingDate = "2026-08-21",
            ConsumedMillilitres = 1250,
            LastServingMillilitres = 250,
            LastServingAt = new DateTimeOffset(2026, 8, 21, 18, 0, 0, TimeSpan.Zero)
        };
        DateTimeOffset today = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        HydrationViewModel viewModel = new(settings, policy, localizer, today);

        Assert.Equal(0, viewModel.ConsumedMillilitres);
        Assert.False(viewModel.CanUndo);
    }

    [Fact]
    public void StateCanBePersistedAndRestored()
    {
        DateTimeOffset now = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        HydrationSettings settings = new();
        HydrationViewModel viewModel = new(settings, policy, localizer, now);
        viewModel.Add(500, settings, now);
        viewModel.WriteState(settings);

        HydrationViewModel restored = new(settings, policy, localizer, now.AddMinutes(5));

        Assert.Equal(500, restored.ConsumedMillilitres);
        Assert.True(restored.CanUndo);
    }

    [Fact]
    public void ResetDayClearsIntakeAndUndoHistory()
    {
        DateTimeOffset now = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        HydrationSettings settings = new();
        HydrationViewModel viewModel = new(settings, policy, localizer, now);
        viewModel.Add(750, settings, now);

        viewModel.ResetDay(settings, now);
        viewModel.WriteState(settings);

        Assert.Equal(0, viewModel.ConsumedMillilitres);
        Assert.False(viewModel.CanUndo);
        Assert.Equal(0, settings.ConsumedMillilitres);
        Assert.Equal(0, settings.LastServingMillilitres);
        Assert.Equal(default, settings.LastServingAt);
    }
}
