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

    [Fact]
    public void SelectClockFindsAClockByCityName()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Local", TimeSpan.Zero, "Local", "Local");
        TimeZoneInfo remoteTimeZone = TimeZoneInfo.CreateCustomTimeZone("Eastern Standard Time", TimeSpan.FromHours(-5), "New York", "New York");
        WorldClockViewModel viewModel = new([
            new WorldClockDefinition("Local", "Local time", localTimeZone),
            new WorldClockDefinition("Eastern Standard Time", "New York", remoteTimeZone)
        ]);
        viewModel.Initialize();

        bool selected = viewModel.SelectClock("New York");

        Assert.True(selected);
        Assert.Equal("New York", viewModel.SelectedClock?.DisplayName);
    }

    [Fact]
    public void SetClocksPreservesASelectedClockThatStillExists()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Local", TimeSpan.Zero, "Local", "Local");
        TimeZoneInfo remoteTimeZone = TimeZoneInfo.CreateCustomTimeZone("Remote", TimeSpan.FromHours(5), "Remote", "Remote");
        WorldClockViewModel viewModel = new([
            new WorldClockDefinition("Local", "Local", localTimeZone),
            new WorldClockDefinition("Remote", "Remote", remoteTimeZone)
        ]);
        viewModel.Initialize();
        viewModel.SelectedClock = viewModel.Clocks[1];

        viewModel.SetClocks([
            new WorldClockDefinition("Local", "Local", localTimeZone),
            new WorldClockDefinition("Remote", "Remote", remoteTimeZone)
        ]);

        Assert.Equal("Remote", viewModel.SelectedClock?.Id);
    }

    [Fact]
    public void SetClocksFallsBackToLocalWhenTheSelectionWasRemoved()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Local", TimeSpan.Zero, "Local", "Local");
        TimeZoneInfo remoteTimeZone = TimeZoneInfo.CreateCustomTimeZone("Remote", TimeSpan.FromHours(5), "Remote", "Remote");
        WorldClockViewModel viewModel = new([
            new WorldClockDefinition("Local", "Local", localTimeZone),
            new WorldClockDefinition("Remote", "Remote", remoteTimeZone)
        ]);
        viewModel.Initialize();
        viewModel.SelectedClock = viewModel.Clocks[1];

        viewModel.SetClocks([new WorldClockDefinition("Local", "Local", localTimeZone)]);

        Assert.Same(viewModel.LocalClock, viewModel.SelectedClock);
    }

    [Theory]
    [InlineData("What time is it in Greenland?", "Greenland")]
    [InlineData("Can you show me the time for New York", "New York")]
    [InlineData("Tell me the current time at Tokyo.", "Tokyo")]
    public void ParsesNaturalTimeQueries(string command,
        string expectedLocation)
    {
        bool parsed = WorldClockCommandParser.TryGetLocation(command, out string location);

        Assert.True(parsed);
        Assert.Equal(expectedLocation, location);
    }

    [Fact]
    public void ShowClockAddsAndSelectsATemporaryClock()
    {
        TimeZoneInfo localTimeZone = TimeZoneInfo.CreateCustomTimeZone("Local", TimeSpan.Zero, "Local", "Local");
        TimeZoneInfo remoteTimeZone = TimeZoneInfo.CreateCustomTimeZone("Greenland Standard Time", TimeSpan.FromHours(-2), "Greenland", "Greenland");
        WorldClockViewModel viewModel = new([new WorldClockDefinition("Local", "Local", localTimeZone)]);
        viewModel.Initialize();

        viewModel.ShowClock(new WorldClockDefinition(remoteTimeZone.Id, "Greenland", remoteTimeZone));

        Assert.Equal("Greenland", viewModel.SelectedClock?.DisplayName);
        Assert.Equal(2, viewModel.Clocks.Count);
    }

    private static WorldClockViewModel CreateViewModel(TimeSpan offset)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone("Test", offset, "Test", "Test");
        return new WorldClockViewModel([new WorldClockDefinition("Local", "Local", timeZone)]);
    }
}
