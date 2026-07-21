namespace Glance.RemovableDevices;

public interface IRemovableDeviceService
{
    IReadOnlyList<RemovableDevice> GetDevices();

    bool TryOpen(RemovableDevice device);

    bool TryEject(RemovableDevice device);
}
