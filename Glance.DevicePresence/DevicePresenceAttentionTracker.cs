namespace Glance.DevicePresence;

public sealed class DevicePresenceAttentionTracker
{
    private readonly Dictionary<string, byte> batteryLevels = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> deviceIds = new(StringComparer.OrdinalIgnoreCase);
    private bool hasSnapshot;

    public ConnectedBluetoothDevice? Update(IReadOnlyList<ConnectedBluetoothDevice> devices, byte lowBatteryThreshold)
    {
        ConnectedBluetoothDevice? attentionDevice = null;

        if (hasSnapshot)
        {
            attentionDevice = devices.FirstOrDefault(device => !deviceIds.Contains(device.Id)) ?? devices.FirstOrDefault(device => HasCrossedLowBatteryThreshold(device, lowBatteryThreshold));
        }

        EstablishBaseline(devices);
        return attentionDevice;
    }

    public void EstablishBaseline(IReadOnlyList<ConnectedBluetoothDevice> devices)
    {
        deviceIds = devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        batteryLevels.Clear();

        foreach (ConnectedBluetoothDevice device in devices)
        {
            if (device.BatteryLevel is byte batteryLevel)
            {
                batteryLevels[device.Id] = batteryLevel;
            }
        }

        hasSnapshot = true;
    }

    private bool HasCrossedLowBatteryThreshold(ConnectedBluetoothDevice device, byte lowBatteryThreshold) =>
        device.BatteryLevel is byte batteryLevel &&
        batteryLevel <= lowBatteryThreshold &&
        batteryLevels.TryGetValue(device.Id, out byte previousBatteryLevel) &&
        previousBatteryLevel > lowBatteryThreshold;
}
