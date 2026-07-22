namespace Glance.Power;

public sealed class PowerSettings
{
    public double CriticalBatteryThreshold { get; set; } = 10;

    public double LowBatteryThreshold { get; set; } = 20;
}
