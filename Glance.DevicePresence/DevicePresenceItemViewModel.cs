using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.DevicePresence;

public partial class DevicePresenceItemViewModel :
    ObservableObject
{
    [ObservableProperty]
    private string displayName;

    [ObservableProperty]
    private string detail;

    [ObservableProperty]
    private string compactText;

    [ObservableProperty]
    private string glyph;

    public DevicePresenceItemViewModel(
        ConnectedBluetoothDevice device,
        string displayName,
        string detail)
    {
        Device = device;
        this.displayName = displayName;
        this.detail = detail;
        compactText = GetCompactText(displayName, device.BatteryLevel);
        glyph = GetGlyph(device.Kind);
    }

    public ConnectedBluetoothDevice Device { get; private set; }

    public void Update(
        ConnectedBluetoothDevice device,
        string displayName,
        string detail)
    {
        Device = device;
        DisplayName = displayName;
        Detail = detail;
        CompactText = GetCompactText(displayName, device.BatteryLevel);
        Glyph = GetGlyph(device.Kind);
    }

    private static string GetCompactText(string displayName, byte? batteryLevel) =>
        batteryLevel is null
            ? displayName
            : $"{displayName}  ·  {batteryLevel}%";

    private static string GetGlyph(BluetoothDeviceKind kind) => kind switch
    {
        BluetoothDeviceKind.Audio => "\uE7F6",
        BluetoothDeviceKind.Computer => "\uE770",
        BluetoothDeviceKind.Phone => "\uE8EA",
        BluetoothDeviceKind.Keyboard => "\uE765",
        BluetoothDeviceKind.Mouse => "\uE962",
        BluetoothDeviceKind.GameController => "\uE7FC",
        BluetoothDeviceKind.Wearable => "\uE95A",
        _ => "\uE702"
    };
}
