using Glance.Application.Abstractions;

namespace Glance.RemovableDevices.Tests;

public sealed class RemovableDevicesViewModelTests
{
    [Fact]
    public void Constructor_ShowsEmptyState()
    {
        RemovableDevicesViewModel viewModel = new(new FakeLocalizer());

        Assert.False(viewModel.HasDevices);
        Assert.Empty(viewModel.Devices);
        Assert.Null(viewModel.SelectedDevice);
        Assert.Equal("No removable devices", viewModel.CompactStatusText);
    }

    [Fact]
    public void Update_CreatesOnePagePerDevice()
    {
        RemovableDevicesViewModel viewModel = new(new FakeLocalizer());

        viewModel.Update([CreateDevice("usb-b", "E:\\", "Work drive"), CreateDevice("usb-a", "F:\\", "Archive")]);

        Assert.True(viewModel.HasDevices);
        Assert.Equal(2, viewModel.Devices.Count);
        Assert.Equal("Archive", viewModel.Devices[0].DisplayName);
        Assert.Equal("Archive", viewModel.SelectedDevice?.DisplayName);
    }

    [Fact]
    public void Update_SelectsNewlyConnectedDevice()
    {
        RemovableDevicesViewModel viewModel = new(new FakeLocalizer());
        viewModel.Update([CreateDevice("usb-a", "E:\\", "Archive")]);

        viewModel.Update([CreateDevice("usb-a", "E:\\", "Archive"), CreateDevice("usb-b", "F:\\", "Work drive")], "usb-b");

        Assert.Equal("usb-b", viewModel.SelectedDevice?.Device.Id);
        Assert.Equal("Work drive  ·  F:", viewModel.CompactStatusText);
    }

    [Fact]
    public void Update_PreservesSelectedDeviceAndItemInstance()
    {
        RemovableDevicesViewModel viewModel = new(new FakeLocalizer());
        viewModel.Update([CreateDevice("usb-a", "E:\\", "Archive"), CreateDevice("usb-b", "F:\\", "Work drive")]);
        viewModel.SelectedDevice = viewModel.Devices[1];
        RemovableDeviceItemViewModel selected = viewModel.SelectedDevice;

        viewModel.Update([CreateDevice("usb-a", "E:\\", "Archive"), CreateDevice("usb-b", "F:\\", "Work drive", freeBytes: 256)]);

        Assert.Same(selected, viewModel.SelectedDevice);
        Assert.Contains("256 B free", selected.Detail);
    }

    [Fact]
    public void Update_RemovingSelectedDeviceFallsBackToRemainingDevice()
    {
        RemovableDevicesViewModel viewModel = new(new FakeLocalizer());
        viewModel.Update([CreateDevice("usb-a", "E:\\", "Archive"), CreateDevice("usb-b", "F:\\", "Work drive")]);
        viewModel.SelectedDevice = viewModel.Devices[1];

        viewModel.Update([CreateDevice("usb-a", "E:\\", "Archive")]);

        Assert.Single(viewModel.Devices);
        Assert.Equal("usb-a", viewModel.SelectedDevice?.Device.Id);
    }

    [Fact]
    public void DevicePage_ExposesStorageUsageAndFunctionActions()
    {
        RemovableDevicesViewModel viewModel = new(new FakeLocalizer());
        RemovableDevice device = CreateDevice("usb-a", "E:\\", "Archive", totalBytes: 1024, freeBytes: 256);
        RemovableDevice? opened = null;
        RemovableDevice? ejected = null;
        viewModel.OpenRequested += (_, value) => opened = value;
        viewModel.EjectRequested += (_, value) => ejected = value;

        viewModel.Update([device]);
        viewModel.SelectedDevice!.Open();
        viewModel.SelectedDevice.Eject();

        Assert.Equal(device, opened);
        Assert.Equal(device, ejected);
        Assert.Equal(75, viewModel.SelectedDevice.UsagePercent);
        Assert.Equal("256 B free of 1 KB", viewModel.SelectedDevice.Detail);
    }

    private static RemovableDevice CreateDevice(
        string id,
        string rootPath,
        string name,
        long totalBytes = 1024,
        long freeBytes = 512) =>
        new(id, rootPath, name, totalBytes, freeBytes, true);

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => key switch
        {
            "NoDevices" => "No removable devices",
            "RemovableDrive" => "Removable drive",
            "StorageDetail" => string.Format("{0} free of {1}", arguments),
            "OpenFailed" => "Could not open this device",
            "EjectFailed" => "Could not safely eject this device",
            _ => key
        };
    }
}
