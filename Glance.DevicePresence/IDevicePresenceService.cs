namespace Glance.DevicePresence;

public interface IDevicePresenceService
{
    event EventHandler? DevicesChanged;

    bool IsReady { get; }

    IReadOnlyList<ConnectedBluetoothDevice> GetConnectedDevices();
}
