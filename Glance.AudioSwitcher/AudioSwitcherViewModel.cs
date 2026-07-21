using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.AudioSwitcher;

public partial class AudioSwitcherViewModel : ObservableObject
{
    private readonly IAudioDeviceService audioDeviceService;
    private readonly ITextLocalizer localizer;
    private IReadOnlyList<AudioOutputDevice> devices = [];
    private int selectedIndex = -1;

    [ObservableProperty]
    private string currentDeviceName;

    [ObservableProperty]
    private string devicePositionText = string.Empty;

    [ObservableProperty]
    private bool canSwitch;

    public AudioSwitcherViewModel(
        IAudioDeviceService audioDeviceService,
        ITextLocalizer localizer)
    {
        this.audioDeviceService = audioDeviceService;
        this.localizer = localizer;
        currentDeviceName = localizer.GetText("NoOutputDevice");

        Refresh();
    }

    public void Refresh()
    {
        devices = audioDeviceService.GetOutputDevices();
        selectedIndex = FindSelectedIndex(devices);

        if (selectedIndex < 0)
        {
            CurrentDeviceName = localizer.GetText("NoOutputDevice");
            DevicePositionText = string.Empty;
            CanSwitch = false;
            return;
        }

        CurrentDeviceName = devices[selectedIndex].Name;
        DevicePositionText = $"{selectedIndex + 1} / {devices.Count}";
        CanSwitch = devices.Count > 1;
    }

    public void Previous() => Switch(-1);

    public void Next() => Switch(1);

    private static int FindSelectedIndex(IReadOnlyList<AudioOutputDevice> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (values[index].IsDefault)
            {
                return index;
            }
        }

        return values.Count > 0 ? 0 : -1;
    }

    private void Switch(int offset)
    {
        if (!CanSwitch || selectedIndex < 0)
        {
            return;
        }

        int targetIndex = (selectedIndex + offset + devices.Count) % devices.Count;

        if (audioDeviceService.TrySetDefaultOutput(devices[targetIndex].Id))
        {
            Refresh();
        }
    }
}
