using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.AudioSwitcher;

public partial class AudioSwitcherViewModel : ObservableObject
{
    private readonly IAudioDeviceService audioDeviceService;
    private readonly ITextLocalizer localizer;
    private bool isRefreshing;
    private string? selectedDeviceId;

    [ObservableProperty]
    private string currentDeviceName;

    [ObservableProperty]
    private bool hasDevices;

    [ObservableProperty]
    private AudioOutputDeviceItemViewModel? selectedDevice;

    public AudioSwitcherViewModel(
        IAudioDeviceService audioDeviceService,
        ITextLocalizer localizer)
    {
        this.audioDeviceService = audioDeviceService;
        this.localizer = localizer;
        currentDeviceName = localizer.GetText("NoOutputDevice");

        Refresh();
    }

    public ObservableCollection<AudioOutputDeviceItemViewModel> Devices { get; } = [];

    public void Refresh()
    {
        IReadOnlyList<AudioOutputDevice> devices = audioDeviceService.GetOutputDevices();
        Dictionary<string, AudioOutputDeviceItemViewModel> existing = Devices.ToDictionary(device => device.Id, StringComparer.OrdinalIgnoreCase);
        List<AudioOutputDeviceItemViewModel> ordered = [];

        foreach (AudioOutputDevice device in devices)
        {
            if (existing.TryGetValue(device.Id, out AudioOutputDeviceItemViewModel? item))
            {
                item.Update(device);
                ordered.Add(item);
            }
            else
            {
                ordered.Add(new AudioOutputDeviceItemViewModel(device, audioDeviceService));
            }
        }

        Synchronize(ordered);
        AudioOutputDeviceItemViewModel? currentDevice = Devices.FirstOrDefault(device => device.IsDefault) ?? Devices.FirstOrDefault();

        isRefreshing = true;
        HasDevices = Devices.Count > 0;
        SelectedDevice = null;
        SelectedDevice = currentDevice;
        selectedDeviceId = currentDevice?.Device.Id;
        CurrentDeviceName = currentDevice?.Name ?? localizer.GetText("NoOutputDevice");
        isRefreshing = false;
    }

    partial void OnSelectedDeviceChanged(AudioOutputDeviceItemViewModel? value)
    {
        if (isRefreshing || value is null || string.Equals(value.Id, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (audioDeviceService.TrySetDefaultOutput(value.Id))
        {
            selectedDeviceId = value.Id;
            CurrentDeviceName = value.Name;

            foreach (AudioOutputDeviceItemViewModel device in Devices)
            {
                device.IsDefault = ReferenceEquals(device, value);
            }

            return;
        }

        Refresh();
    }

    private void Synchronize(IReadOnlyList<AudioOutputDeviceItemViewModel> ordered)
    {
        for (int index = 0; index < ordered.Count; index++)
        {
            AudioOutputDeviceItemViewModel item = ordered[index];

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
}
