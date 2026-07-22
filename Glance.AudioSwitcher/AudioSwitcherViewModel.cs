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
    private AudioOutputDevice? selectedDevice;

    public AudioSwitcherViewModel(
        IAudioDeviceService audioDeviceService,
        ITextLocalizer localizer)
    {
        this.audioDeviceService = audioDeviceService;
        this.localizer = localizer;
        currentDeviceName = localizer.GetText("NoOutputDevice");

        Refresh();
    }

    public ObservableCollection<AudioOutputDevice> Devices { get; } = [];

    public void Refresh()
    {
        IReadOnlyList<AudioOutputDevice> devices = audioDeviceService.GetOutputDevices();
        AudioOutputDevice? currentDevice = devices.FirstOrDefault(device => device.IsDefault) ?? devices.FirstOrDefault();

        isRefreshing = true;
        Devices.Clear();

        foreach (AudioOutputDevice device in devices)
        {
            Devices.Add(device);
        }

        HasDevices = Devices.Count > 0;
        SelectedDevice = null;
        SelectedDevice = currentDevice;
        selectedDeviceId = currentDevice?.Id;
        CurrentDeviceName = currentDevice?.Name ?? localizer.GetText("NoOutputDevice");
        isRefreshing = false;
    }

    partial void OnSelectedDeviceChanged(AudioOutputDevice? value)
    {
        if (isRefreshing || value is null || string.Equals(value.Id, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (audioDeviceService.TrySetDefaultOutput(value.Id))
        {
            selectedDeviceId = value.Id;
            CurrentDeviceName = value.Name;
            return;
        }

        Refresh();
    }
}
