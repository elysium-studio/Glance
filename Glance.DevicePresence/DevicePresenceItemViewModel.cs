using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.DevicePresence;

public sealed partial class DevicePresenceItemViewModel :
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

    [ObservableProperty]
    private int batteryLevel;

    public DevicePresenceItemViewModel(
        ConnectedBluetoothDevice device,
        string displayName,
        string detail)
    {
        Device = device;
        this.displayName = displayName;
        this.detail = detail;
        compactText = displayName;
        glyph = GetGlyph(device.Kind);
        batteryLevel = device.BatteryLevel ?? -1;
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
        CompactText = displayName;
        Glyph = GetGlyph(device.Kind);
        BatteryLevel = device.BatteryLevel ?? -1;
    }

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
