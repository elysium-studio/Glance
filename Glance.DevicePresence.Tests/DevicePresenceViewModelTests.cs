using Glance.Application.Abstractions;

namespace Glance.DevicePresence.Tests;

public sealed class DevicePresenceViewModelTests
{
    [Fact]
    public void Constructor_ShowsEmptyState()
    {
        DevicePresenceViewModel viewModel = new(new FakeLocalizer());

        Assert.False(viewModel.HasDevices);
        Assert.Empty(viewModel.Devices);
        Assert.Null(viewModel.SelectedDevice);
        Assert.Equal("No Bluetooth devices connected", viewModel.CompactStatusText);
        Assert.Equal("\uE702", viewModel.SelectedDeviceGlyph);
        Assert.Equal(-1, viewModel.SelectedDeviceBatteryLevel);
    }

    [Fact]
    public void Update_CreatesOnePagePerConnectedDevice()
    {
        DevicePresenceViewModel viewModel = new(new FakeLocalizer());

        viewModel.Update([CreateDevice("mouse", "Surface Mouse", BluetoothDeviceKind.Mouse), CreateDevice("audio", "Headphones", BluetoothDeviceKind.Audio, 72)]);

        Assert.True(viewModel.HasDevices);
        Assert.Equal(2, viewModel.Devices.Count);
        Assert.Equal("Headphones", viewModel.SelectedDevice?.DisplayName);
        Assert.Equal("Headphones", viewModel.CompactStatusText);
        Assert.Equal(72, viewModel.SelectedDeviceBatteryLevel);
        Assert.Equal("\uE7F6", viewModel.SelectedDeviceGlyph);
    }

    [Fact]
    public void Update_SelectsPreferredNewDevice()
    {
        DevicePresenceViewModel viewModel = new(new FakeLocalizer());
        viewModel.Update([CreateDevice("mouse", "Mouse", BluetoothDeviceKind.Mouse)]);

        viewModel.Update([CreateDevice("mouse", "Mouse", BluetoothDeviceKind.Mouse), CreateDevice("phone", "Phone", BluetoothDeviceKind.Phone, 45)], "phone");

        Assert.Equal("phone", viewModel.SelectedDevice?.Device.Id);
        Assert.Equal("Phone", viewModel.CompactStatusText);
        Assert.Equal(45, viewModel.SelectedDeviceBatteryLevel);
        Assert.Equal("\uE8EA", viewModel.SelectedDeviceGlyph);
    }

    [Fact]
    public void Update_PreservesSelectionAndUpdatesBattery()
    {
        DevicePresenceViewModel viewModel = new(new FakeLocalizer());
        viewModel.Update([CreateDevice("audio", "Headphones", BluetoothDeviceKind.Audio, 80), CreateDevice("mouse", "Mouse", BluetoothDeviceKind.Mouse)]);
        viewModel.SelectedDevice = viewModel.Devices[1];
        DevicePresenceItemViewModel selected = viewModel.SelectedDevice;

        viewModel.Update([CreateDevice("audio", "Headphones", BluetoothDeviceKind.Audio, 75), CreateDevice("mouse", "Mouse", BluetoothDeviceKind.Mouse, 60)]);

        Assert.Same(selected, viewModel.SelectedDevice);
        Assert.Equal("Connected", selected.Detail);
        Assert.Equal(60, selected.BatteryLevel);
        Assert.Equal("Mouse", viewModel.CompactStatusText);
        Assert.Equal(60, viewModel.SelectedDeviceBatteryLevel);
    }

    [Fact]
    public void Update_RemovesDisconnectedDevice()
    {
        DevicePresenceViewModel viewModel = new(new FakeLocalizer());
        viewModel.Update([CreateDevice("audio", "Headphones", BluetoothDeviceKind.Audio), CreateDevice("mouse", "Mouse", BluetoothDeviceKind.Mouse)]);
        viewModel.SelectedDevice = viewModel.Devices[1];

        viewModel.Update([CreateDevice("audio", "Headphones", BluetoothDeviceKind.Audio)]);

        Assert.Single(viewModel.Devices);
        Assert.Equal("audio", viewModel.SelectedDevice?.Device.Id);
    }

    private static ConnectedBluetoothDevice CreateDevice(
        string id,
        string name,
        BluetoothDeviceKind kind,
        byte? batteryLevel = null) =>
        new(id, name, kind, batteryLevel);

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "NoDevices" => "No Bluetooth devices connected",
            "BluetoothDevice" => "Bluetooth device",
            "Connected" => "Connected",
            "ConnectedWithBattery" => $"Connected · {arguments[0]}% battery",
            _ => key
        };
    }
}
