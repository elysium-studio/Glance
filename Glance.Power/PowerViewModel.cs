using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.Power;

public partial class PowerViewModel : ObservableObject
{
    public string Title => "Power";

    [ObservableProperty]
    private double chargePercent = 100;

    [ObservableProperty]
    private string chargeText = "AC";

    [ObservableProperty]
    private string compactStatusText = "Connected";

    [ObservableProperty]
    private bool hasBattery;

    [ObservableProperty]
    private string statusText = "Power status";

    [ObservableProperty]
    private string detailText = "Unavailable";

    public void Update(PowerSnapshot snapshot)
    {
        bool hasBattery = snapshot.BatteryState != BatteryState.NotPresent;
        int percentage = Math.Clamp(snapshot.ChargePercent, 0, 100);

        HasBattery = hasBattery;
        ChargePercent = hasBattery ? percentage : 100;
        ChargeText = hasBattery ? $"{percentage}%" : "AC";

        (CompactStatusText, StatusText) = snapshot.BatteryState switch
        {
            BatteryState.Charging => ("Charging", "Charging"),
            BatteryState.Discharging => ("On battery", "Using battery"),
            BatteryState.Idle when percentage >= 99 => ("Charged", "Fully charged"),
            BatteryState.Idle => ("Connected", "Connected to power"),
            _ => ("Connected", "Power status")
        };

        DetailText = snapshot.BatteryState switch
        {
            BatteryState.NotPresent => "Unavailable",
            BatteryState.Charging => "Connected and charging",
            BatteryState.Idle when percentage >= 99 => "Battery is fully charged",
            BatteryState.Idle => "Connected to external power",
            BatteryState.Discharging => FormatRemainingTime(snapshot.RemainingTime),
            _ => "Power status unavailable"
        };
    }

    private static string FormatRemainingTime(TimeSpan? remainingTime)
    {
        if (remainingTime is not { } value || value <= TimeSpan.Zero)
        {
            return "Estimating time remaining";
        }

        int hours = (int)value.TotalHours;
        int minutes = value.Minutes;

        return hours switch
        {
            > 0 when minutes > 0 => $"{hours} hr {minutes} min remaining",
            > 0 => $"{hours} hr remaining",
            _ => $"{Math.Max(1, minutes)} min remaining"
        };
    }
}
