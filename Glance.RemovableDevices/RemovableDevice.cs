namespace Glance.RemovableDevices;

public sealed record RemovableDevice(
    string Id,
    string RootPath,
    string DisplayName,
    long TotalBytes,
    long FreeBytes,
    bool CanEject);
