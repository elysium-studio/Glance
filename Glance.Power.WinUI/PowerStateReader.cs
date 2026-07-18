using Microsoft.Windows.System.Power;
using System;

namespace Glance.Power.WinUI;

internal static class PowerStateReader
{
    public static PowerSnapshot Read()
    {
        try
        {
            BatteryStatus status = PowerManager.BatteryStatus;
            bool hasBattery = status != BatteryStatus.NotPresent;
            bool isOnBattery = PowerManager.PowerSourceKind == PowerSourceKind.DC;
            int percentage = hasBattery
                ? Math.Clamp(PowerManager.RemainingChargePercent, 0, 100)
                : 100;
            TimeSpan remaining = PowerManager.RemainingDischargeTime;

            return new PowerSnapshot(
                Map(status),
                percentage,
                isOnBattery,
                isOnBattery && remaining > TimeSpan.Zero ? remaining : null);
        }
        catch
        {
            return new PowerSnapshot(BatteryState.NotPresent, 100, false, null);
        }
    }

    private static BatteryState Map(BatteryStatus status) => status switch
    {
        BatteryStatus.Charging => BatteryState.Charging,
        BatteryStatus.Discharging => BatteryState.Discharging,
        BatteryStatus.Idle => BatteryState.Idle,
        _ => BatteryState.NotPresent
    };
}
