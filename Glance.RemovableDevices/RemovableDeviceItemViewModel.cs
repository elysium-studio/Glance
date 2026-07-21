using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.RemovableDevices;

public partial class RemovableDeviceItemViewModel :
    ObservableObject
{
    private readonly Action<RemovableDevice> eject;
    private readonly Action<RemovableDevice> open;

    [ObservableProperty]
    private string displayName;

    [ObservableProperty]
    private string driveLetter;

    [ObservableProperty]
    private string detail;

    [ObservableProperty]
    private string compactText;

    [ObservableProperty]
    private double usagePercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEject))]
    private bool isBusy;

    public RemovableDeviceItemViewModel(
        RemovableDevice device,
        string displayName,
        string detail,
        Action<RemovableDevice> open,
        Action<RemovableDevice> eject)
    {
        Device = device;
        this.displayName = displayName;
        driveLetter = GetDriveLetter(device.RootPath);
        this.detail = detail;
        compactText = GetCompactText(displayName, driveLetter);
        usagePercent = GetUsagePercent(device);
        this.open = open;
        this.eject = eject;
    }

    public RemovableDevice Device { get; private set; }

    public bool CanEject => Device.CanEject && !IsBusy;

    public void Open() =>
        open(Device);

    public void Eject() =>
        eject(Device);

    public void Update(
        RemovableDevice device,
        string displayName,
        string detail)
    {
        Device = device;
        DisplayName = displayName;
        DriveLetter = GetDriveLetter(device.RootPath);
        Detail = detail;
        CompactText = GetCompactText(displayName, DriveLetter);
        UsagePercent = GetUsagePercent(device);
        OnPropertyChanged(nameof(CanEject));
    }

    private static string GetDriveLetter(string rootPath) =>
        rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string GetCompactText(
        string displayName,
        string driveLetter) =>
        $"{displayName}  ·  {driveLetter}";

    private static double GetUsagePercent(RemovableDevice device) =>
        device.TotalBytes <= 0
            ? 0
            : Math.Clamp((device.TotalBytes - device.FreeBytes) * 100d / device.TotalBytes, 0, 100);
}
