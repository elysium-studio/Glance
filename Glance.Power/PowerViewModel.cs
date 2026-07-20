using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Power;

public partial class PowerViewModel : ObservableObject
{
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private double chargePercent = 100;

    [ObservableProperty]
    private string chargeText = "AC";

    [ObservableProperty]
    private string compactStatusText;

    [ObservableProperty]
    private bool hasBattery;

    [ObservableProperty]
    private string statusText;

    [ObservableProperty]
    private string detailText;

    public PowerViewModel(ITextLocalizer localizer)
    {
        this.localizer = localizer;
        compactStatusText = localizer.GetText("Connected");
        statusText = localizer.GetText("PowerStatus");
        detailText = localizer.GetText("Unavailable");
    }

    public string Title => localizer.GetText("ModuleTitle");

    public void Update(PowerSnapshot snapshot)
    {
        bool hasBattery = snapshot.BatteryState != BatteryState.NotPresent;
        int percentage = Math.Clamp(snapshot.ChargePercent, 0, 100);

        HasBattery = hasBattery;
        ChargePercent = hasBattery ? percentage : 100;
        ChargeText = hasBattery ? $"{percentage}%" : "AC";

        (CompactStatusText, StatusText) = snapshot.BatteryState switch
        {
            BatteryState.Charging => (
                localizer.GetText("Charging"),
                localizer.GetText("Charging")),
            BatteryState.Discharging => (
                localizer.GetText("OnBattery"),
                localizer.GetText("UsingBattery")),
            BatteryState.Idle when percentage >= 99 => (
                localizer.GetText("Charged"),
                localizer.GetText("FullyCharged")),
            BatteryState.Idle => (
                localizer.GetText("Connected"),
                localizer.GetText("ConnectedToPower")),
            _ => (
                localizer.GetText("Connected"),
                localizer.GetText("PowerStatus"))
        };

        DetailText = snapshot.BatteryState switch
        {
            BatteryState.NotPresent => localizer.GetText("Unavailable"),
            BatteryState.Charging => localizer.GetText("ConnectedAndCharging"),
            BatteryState.Idle when percentage >= 99 => localizer.GetText("BatteryFullyCharged"),
            BatteryState.Idle => localizer.GetText("ConnectedToExternalPower"),
            BatteryState.Discharging => FormatRemainingTime(snapshot.RemainingTime),
            _ => localizer.GetText("PowerStatusUnavailable")
        };
    }

    private string FormatRemainingTime(TimeSpan? remainingTime)
    {
        if (remainingTime is not { } value || value <= TimeSpan.Zero)
        {
            return localizer.GetText("EstimatingRemainingTime");
        }

        int hours = (int)value.TotalHours;
        int minutes = value.Minutes;

        return hours switch
        {
            > 0 when minutes > 0 => localizer.GetText(
                "HoursMinutesRemaining",
                hours,
                minutes),
            > 0 => localizer.GetText("HoursRemaining", hours),
            _ => localizer.GetText("MinutesRemaining", Math.Max(1, minutes))
        };
    }
}
