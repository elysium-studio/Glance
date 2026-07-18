namespace Glance.Power;

public enum BatteryState
{
    NotPresent,
    Discharging,
    Idle,
    Charging
}

public sealed record PowerSnapshot(
    BatteryState BatteryState,
    int ChargePercent,
    bool IsOnBattery,
    TimeSpan? RemainingTime);
