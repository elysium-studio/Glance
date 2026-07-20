using Glance.Application.Abstractions;

namespace Glance.Power.Tests;

public sealed class PowerViewModelTests
{
    [Fact]
    public void Constructor_UsesLocalizedDefaults()
    {
        PowerViewModel viewModel = new(new TestTextLocalizer());

        Assert.Equal("ModuleTitle", viewModel.Title);
        Assert.Equal("Connected", viewModel.CompactStatusText);
        Assert.Equal("PowerStatus", viewModel.StatusText);
        Assert.Equal("Unavailable", viewModel.DetailText);
        Assert.Equal("AC", viewModel.ChargeText);
    }

    [Fact]
    public void Update_WithoutBattery_ShowsDesktopPowerState()
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.NotPresent, 0, false, null));

        Assert.False(viewModel.HasBattery);
        Assert.Equal(100, viewModel.ChargePercent);
        Assert.Equal("AC", viewModel.ChargeText);
        Assert.Equal("Connected", viewModel.CompactStatusText);
        Assert.Equal("PowerStatus", viewModel.StatusText);
        Assert.Equal("Unavailable", viewModel.DetailText);
    }

    [Fact]
    public void Update_Charging_ShowsChargeState()
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.Charging, 42, false, null));

        Assert.True(viewModel.HasBattery);
        Assert.Equal(42, viewModel.ChargePercent);
        Assert.Equal("42%", viewModel.ChargeText);
        Assert.Equal("Charging", viewModel.CompactStatusText);
        Assert.Equal("Charging", viewModel.StatusText);
        Assert.Equal("ConnectedAndCharging", viewModel.DetailText);
    }

    [Fact]
    public void Update_Discharging_ShowsBatteryStateAndRemainingTime()
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.Discharging, 73, true, new TimeSpan(2, 15, 0)));

        Assert.Equal("OnBattery", viewModel.CompactStatusText);
        Assert.Equal("UsingBattery", viewModel.StatusText);
        Assert.Equal("HoursMinutesRemaining(2,15)", viewModel.DetailText);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    public void Update_IdleAndFull_ShowsFullyCharged(int percentage)
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.Idle, percentage, false, null));

        Assert.Equal("Charged", viewModel.CompactStatusText);
        Assert.Equal("FullyCharged", viewModel.StatusText);
        Assert.Equal("BatteryFullyCharged", viewModel.DetailText);
    }

    [Fact]
    public void Update_IdleAndNotFull_ShowsExternalPower()
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.Idle, 61, false, null));

        Assert.Equal("Connected", viewModel.CompactStatusText);
        Assert.Equal("ConnectedToPower", viewModel.StatusText);
        Assert.Equal("ConnectedToExternalPower", viewModel.DetailText);
    }

    [Theory]
    [InlineData(-10, 0, "0%")]
    [InlineData(140, 100, "100%")]
    public void Update_ClampsBatteryPercentage(int input, int expected, string expectedText)
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.Discharging, input, true, null));

        Assert.Equal(expected, viewModel.ChargePercent);
        Assert.Equal(expectedText, viewModel.ChargeText);
    }

    [Theory]
    [MemberData(nameof(RemainingTimeCases))]
    public void Update_FormatsRemainingTime(TimeSpan? remaining, string expected)
    {
        PowerViewModel viewModel = CreateViewModel();

        viewModel.Update(new(BatteryState.Discharging, 50, true, remaining));

        Assert.Equal(expected, viewModel.DetailText);
    }

    public static TheoryData<TimeSpan?, string> RemainingTimeCases => new()
    {
        { null, "EstimatingRemainingTime" },
        { TimeSpan.Zero, "EstimatingRemainingTime" },
        { TimeSpan.FromMinutes(35), "MinutesRemaining(35)" },
        { TimeSpan.FromHours(3), "HoursRemaining(3)" },
        { new TimeSpan(3, 20, 0), "HoursMinutesRemaining(3,20)" }
    };

    private static PowerViewModel CreateViewModel() => new(new TestTextLocalizer());

    private sealed class TestTextLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) =>
            arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
