using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.RemovableDevices;

public partial class RemovableDevicesViewModel :
    ObservableObject
{
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private bool hasDevices;

    [ObservableProperty]
    private string compactStatusText;

    [ObservableProperty]
    private RemovableDeviceItemViewModel? selectedDevice;

    public RemovableDevicesViewModel(ITextLocalizer localizer)
    {
        this.localizer = localizer;
        compactStatusText = localizer.GetText("NoDevices");
    }

    public ObservableCollection<RemovableDeviceItemViewModel> Devices { get; } = [];

    public event EventHandler<RemovableDevice>? OpenRequested;

    public event EventHandler<RemovableDevice>? EjectRequested;

    public void Update(
        IReadOnlyList<RemovableDevice> devices,
        string? preferredDeviceId = null)
    {
        string? selectedId = SelectedDevice?.Device.Id;
        Dictionary<string, RemovableDeviceItemViewModel> existing = Devices.ToDictionary(item => item.Device.Id, StringComparer.OrdinalIgnoreCase);
        List<RemovableDeviceItemViewModel> ordered = [];

        foreach (RemovableDevice device in devices.OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(device => device.RootPath, StringComparer.OrdinalIgnoreCase))
        {
            string displayName = GetDisplayName(device);
            string detail = GetDetail(device);

            if (existing.TryGetValue(device.Id, out RemovableDeviceItemViewModel? item))
            {
                item.Update(device, displayName, detail);
                ordered.Add(item);
            }
            else
            {
                ordered.Add(new RemovableDeviceItemViewModel(device, displayName, detail, Open, Eject));
            }
        }

        Synchronize(ordered);
        HasDevices = Devices.Count > 0;
        SelectedDevice = Find(preferredDeviceId) ?? Find(selectedId) ?? Devices.FirstOrDefault();
        UpdateCompactStatus();
    }

    public void SetBusy(
        string deviceId,
        bool isBusy)
    {
        RemovableDeviceItemViewModel? item = Find(deviceId);

        if (item is not null)
        {
            item.IsBusy = isBusy;
        }
    }

    public void ShowOpenFailure(string deviceId) =>
        ShowFailure(deviceId, localizer.GetText("OpenFailed"));

    public void ShowEjectFailure(string deviceId) =>
        ShowFailure(deviceId, localizer.GetText("EjectFailed"));

    partial void OnSelectedDeviceChanged(RemovableDeviceItemViewModel? value) =>
        UpdateCompactStatus();

    private void Open(RemovableDevice device) =>
        OpenRequested?.Invoke(this, device);

    private void Eject(RemovableDevice device) =>
        EjectRequested?.Invoke(this, device);

    private RemovableDeviceItemViewModel? Find(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId)
            ? null
            : Devices.FirstOrDefault(item => string.Equals(item.Device.Id, deviceId, StringComparison.OrdinalIgnoreCase));

    private string GetDisplayName(RemovableDevice device) =>
        string.IsNullOrWhiteSpace(device.DisplayName)
            ? localizer.GetText("RemovableDrive")
            : device.DisplayName;

    private string GetDetail(RemovableDevice device) =>
        localizer.GetText("StorageDetail", FormatSize(device.FreeBytes), FormatSize(device.TotalBytes));

    private void ShowFailure(
        string deviceId,
        string message)
    {
        RemovableDeviceItemViewModel? item = Find(deviceId);

        if (item is not null)
        {
            item.Detail = message;
            SelectedDevice = item;
        }
    }

    private void Synchronize(IReadOnlyList<RemovableDeviceItemViewModel> ordered)
    {
        for (int index = 0; index < ordered.Count; index++)
        {
            RemovableDeviceItemViewModel item = ordered[index];

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

    private void UpdateCompactStatus() =>
        CompactStatusText = SelectedDevice?.CompactText ?? localizer.GetText("NoDevices");

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.#} {units[unit]}";
    }
}
