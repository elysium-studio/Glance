namespace Glance.WorldClock.Tests;

public sealed class WorldClockViewModelTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 8, 1, 13, 5, 0, TimeSpan.Zero);

    [Fact]
    public void RefreshFormatsTwentyFourHourTime()
    {
        WorldClockViewModel viewModel = CreateViewModel(TimeSpan.FromHours(2));

        viewModel.Refresh(TestTime, true);

        Assert.Equal("15:05", viewModel.LocalClock.TimeText);
    }

    [Fact]
    public void RefreshFormatsTwelveHourTime()
    {
        WorldClockViewModel viewModel = CreateViewModel(TimeSpan.FromHours(-4));

        viewModel.Refresh(TestTime, false);

        Assert.StartsWith("9:05", viewModel.LocalClock.TimeText);
    }

    [Fact]
    public void InitializeSelectsTheLocalClock()
    {
        WorldClockViewModel viewModel = CreateViewModel(TimeSpan.Zero);

        viewModel.Initialize();

        Assert.Same(viewModel.LocalClock, viewModel.SelectedClock);
    }

    [Fact]
    public void RefreshPreservesTheSelectedClock()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Local", TimeSpan.Zero, "Local", "Local");
        TimeZoneInfo remoteTimeZone = TimeZoneInfo.CreateCustomTimeZone("Remote", TimeSpan.FromHours(5), "Remote", "Remote");
        WorldClockViewModel viewModel = new([
            new WorldClockDefinition("Local", "Local", localTimeZone),
            new WorldClockDefinition("Remote", "Remote", remoteTimeZone)
        ]);
        viewModel.Initialize();
        viewModel.SelectedClock = viewModel.Clocks[1];

        viewModel.Refresh(TestTime, true);

        Assert.Same(viewModel.Clocks[1], viewModel.SelectedClock);
        Assert.Equal("18:05", viewModel.SelectedClock.TimeText);
    }

    private static WorldClockViewModel CreateViewModel(TimeSpan offset)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone("Test", offset, "Test", "Test");
        return new WorldClockViewModel([new WorldClockDefinition("Local", "Local", timeZone)]);
    }
}
