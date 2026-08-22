namespace Glance.Fasting.Tests;

public sealed class FastingViewModelTests
{
    private readonly TestTextLocalizer localizer = new();

    [Fact]
    public void StartUsesPreferredPlan()
    {
        DateTimeOffset now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        FastingSettings settings = new() { Plan = FastingPlan.EighteenSix };
        FastingViewModel viewModel = new(settings, localizer, now);

        viewModel.Start(settings, now);

        Assert.True(viewModel.IsFasting);
        Assert.Equal(TimeSpan.FromHours(18), viewModel.Remaining);
        Assert.Equal(0, viewModel.Progress);
    }

    [Fact]
    public void ActiveFastKeepsOriginalDurationWhenPreferenceChanges()
    {
        DateTimeOffset now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        FastingSettings settings = new() { Plan = FastingPlan.TwelveTwelve };
        FastingViewModel viewModel = new(settings, localizer, now);
        viewModel.Start(settings, now);
        settings.Plan = FastingPlan.TwentyFour;

        viewModel.ApplySettings(settings, now.AddHours(1));

        Assert.Equal(TimeSpan.FromHours(11), viewModel.Remaining);
    }

    [Fact]
    public void RefreshCompletesFastOnce()
    {
        DateTimeOffset now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        FastingSettings settings = new() { Plan = FastingPlan.Custom, CustomFastingHours = 1 };
        FastingViewModel viewModel = new(settings, localizer, now);
        viewModel.Start(settings, now);

        bool completed = viewModel.Refresh(now.AddHours(1));
        bool completedAgain = viewModel.Refresh(now.AddHours(2));

        Assert.True(completed);
        Assert.False(completedAgain);
        Assert.Equal(FastingSessionStatus.Completed, viewModel.Status);
        Assert.Equal(1, viewModel.Progress);
    }

    [Fact]
    public void StateCanBePersistedAndRestored()
    {
        DateTimeOffset now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        FastingSettings settings = new() { Plan = FastingPlan.SixteenEight };
        FastingViewModel viewModel = new(settings, localizer, now);
        viewModel.Start(settings, now);
        viewModel.WriteState(settings);

        FastingViewModel restored = new(settings, localizer, now.AddHours(4));

        Assert.True(restored.IsFasting);
        Assert.Equal(TimeSpan.FromHours(12), restored.Remaining);
        Assert.Equal(0.25, restored.Progress, 3);
    }

    [Fact]
    public void ResetClearsSession()
    {
        DateTimeOffset now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        FastingSettings settings = new();
        FastingViewModel viewModel = new(settings, localizer, now);
        viewModel.Start(settings, now);

        viewModel.Reset(now.AddHours(2));
        viewModel.WriteState(settings);

        Assert.Equal(FastingSessionStatus.Ready, viewModel.Status);
        Assert.Equal(default, settings.StartedAt);
        Assert.Equal(default, settings.EndsAt);
    }

    [Fact]
    public void CompletionAttentionCanBeDeduplicated()
    {
        DateTimeOffset now = new(2026, 8, 22, 8, 0, 0, TimeSpan.Zero);
        FastingViewModel viewModel = new(new FastingSettings(), localizer, now);

        viewModel.MarkCompletionAttentionSent();
        viewModel.MarkCompletionAttentionSent();

        Assert.True(viewModel.CompletionAttentionSent);
    }
}
