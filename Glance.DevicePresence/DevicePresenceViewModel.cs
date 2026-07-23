using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.DevicePresence;

public sealed partial class DevicePresenceViewModel :
    ObservableObject
{
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private bool hasDevices;

    [ObservableProperty]
    private string compactStatusText;

    [ObservableProperty]
    private string selectedDeviceGlyph = "\uE702";

    [ObservableProperty]
    private int selectedDeviceBatteryLevel = -1;

    [ObservableProperty]
    private DevicePresenceItemViewModel? selectedDevice;

    public DevicePresenceViewModel(ITextLocalizer localizer)
    {
        this.localizer = localizer;
        compactStatusText = localizer.GetText("NoDevices");
    }

    public ObservableCollection<DevicePresenceItemViewModel> Devices { get; } = [];

    public void Update(
        IReadOnlyList<ConnectedBluetoothDevice> devices,
        string? preferredDeviceId = null)
    {
        string? selectedId = SelectedDevice?.Device.Id;
        Dictionary<string, DevicePresenceItemViewModel> existing = Devices.ToDictionary(item => item.Device.Id, StringComparer.OrdinalIgnoreCase);
        List<DevicePresenceItemViewModel> ordered = [];

        foreach (ConnectedBluetoothDevice device in devices.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            string displayName = string.IsNullOrWhiteSpace(device.Name) ? localizer.GetText("BluetoothDevice") : device.Name;
            string detail = localizer.GetText("Connected");

            if (existing.TryGetValue(device.Id, out DevicePresenceItemViewModel? item))
            {
                item.Update(device, displayName, detail);
                ordered.Add(item);
            }
            else
            {
                ordered.Add(new DevicePresenceItemViewModel(device, displayName, detail));
            }
        }

        Synchronize(ordered);
        HasDevices = Devices.Count > 0;
        SelectedDevice = Find(preferredDeviceId) ?? Find(selectedId) ?? Devices.FirstOrDefault();
        UpdateSelectedDevice();
    }

    partial void OnSelectedDeviceChanged(DevicePresenceItemViewModel? value) =>
        UpdateSelectedDevice();

    private DevicePresenceItemViewModel? Find(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId)
            ? null
            : Devices.FirstOrDefault(item => string.Equals(item.Device.Id, deviceId, StringComparison.OrdinalIgnoreCase));

    private void Synchronize(IReadOnlyList<DevicePresenceItemViewModel> ordered)
    {
        for (int index = 0; index < ordered.Count; index++)
        {
            DevicePresenceItemViewModel item = ordered[index];

            if (index < Devices.Count && ReferenceEquals(Devices[index], item))
            {
                continue;
            }

            int currentIndex = Devices.IndexOf(item);

            if (currentIndex >= 0)
            {
                Devices.Move(currentIndex, index);
            }
            else
            {
                Devices.Insert(index, item);
            }
        }

        while (Devices.Count > ordered.Count)
        {
            Devices.RemoveAt(Devices.Count - 1);
        }
    }

    private void UpdateSelectedDevice()
    {
        CompactStatusText = SelectedDevice?.CompactText ?? localizer.GetText("NoDevices");
        SelectedDeviceGlyph = SelectedDevice?.Glyph ?? "\uE702";
        SelectedDeviceBatteryLevel = SelectedDevice?.BatteryLevel ?? -1;
    }
}
