using CommunityToolkit.Mvvm.ComponentModel;

namespace Glance.DevicePresence;

public sealed partial class DevicePresenceItemViewModel(ConnectedBluetoothDevice device,
    string displayName,
    string detail) :
    ObservableObject
{
    [ObservableProperty]
    private string displayName = displayName;

    [ObservableProperty]
    private string detail = detail;

    [ObservableProperty]
    private string compactText = displayName;

    [ObservableProperty]
    private string glyph = GetGlyph(device.Kind);

    [ObservableProperty]
    private int batteryLevel = device.BatteryLevel ?? -1;

    public ConnectedBluetoothDevice Device { get; private set; } = device;

    public void Update(ConnectedBluetoothDevice device,
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
