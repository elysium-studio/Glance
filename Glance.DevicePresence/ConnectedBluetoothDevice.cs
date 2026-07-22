namespace Glance.DevicePresence;

public sealed record ConnectedBluetoothDevice(
    string Id,
    string Name,
    BluetoothDeviceKind Kind,
    byte? BatteryLevel);
