namespace Glance.DevicePresence.Tests;

public sealed class DevicePresenceAttentionTrackerTests
{
    [Fact]
    public void InitialSnapshot_DoesNotRequestAttention()
    {
        DevicePresenceAttentionTracker tracker = new();

        ConnectedBluetoothDevice? result = tracker.Update([CreateDevice("mouse", 10)], 20);

        Assert.Null(result);
    }

    [Fact]
    public void FirstAsynchronousBatteryReading_DoesNotRequestAttention()
    {
        DevicePresenceAttentionTracker tracker = new();
        tracker.Update([CreateDevice("mouse", null)], 20);

        ConnectedBluetoothDevice? result = tracker.Update([CreateDevice("mouse", 10)], 20);

        Assert.Null(result);
    }

    [Fact]
    public void BatteryCrossingThreshold_RequestsAttention()
    {
        DevicePresenceAttentionTracker tracker = new();
        tracker.Update([CreateDevice("mouse", 50)], 20);

        ConnectedBluetoothDevice? result = tracker.Update([CreateDevice("mouse", 20)], 20);

        Assert.Equal("mouse", result?.Id);
    }

    [Fact]
    public void NewlyConnectedDevice_RequestsAttention()
    {
        DevicePresenceAttentionTracker tracker = new();
        tracker.Update([CreateDevice("mouse", 50)], 20);

        ConnectedBluetoothDevice? result = tracker.Update([CreateDevice("mouse", 50), CreateDevice("keyboard", 80)], 20);

        Assert.Equal("keyboard", result?.Id);
    }

    [Fact]
    public void EstablishBaseline_DoesNotTreatExistingDeviceAsNew()
    {
        DevicePresenceAttentionTracker tracker = new();
        tracker.Update([CreateDevice("mouse", 50)], 20);
        tracker.EstablishBaseline([CreateDevice("mouse", 15)]);

        ConnectedBluetoothDevice? result = tracker.Update([CreateDevice("mouse", 15)], 20);

        Assert.Null(result);
    }

    private static ConnectedBluetoothDevice CreateDevice(string id, byte? batteryLevel) =>
        new(id, id, BluetoothDeviceKind.Bluetooth, batteryLevel);
}
