using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.PrivacyControls;

public sealed partial class PrivacyControlsViewModel :
    ObservableObject
{
    private readonly IMicrophoneService microphoneService;
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private string deviceName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isActive;

    public PrivacyControlsViewModel(
        IMicrophoneService microphoneService,
        ITextLocalizer localizer)
    {
        this.microphoneService = microphoneService;
        this.localizer = localizer;
        deviceName = localizer.GetText("NoMicrophone");

        Refresh();
    }

    public string StatusText => !IsAvailable
        ? localizer.GetText("NoMicrophone")
        : IsMuted
            ? localizer.GetText("MicrophoneMuted")
            : IsActive
                ? localizer.GetText("MicrophoneActive")
                : localizer.GetText("MicrophoneReady");

    public string ToggleGlyph => IsMuted ? "\uE74F" : "\uE720";

    public void Refresh() =>
        Update(microphoneService.GetState());

    public void ToggleMute()
    {
        if (IsAvailable && microphoneService.TrySetMuted(!IsMuted))
        {
            Refresh();
        }
    }

    public void Update(MicrophoneState state)
    {
        double level = state.IsAvailable && !state.IsMuted
            ? NormalizeLevel(state.PeakLevel)
            : 0;

        DeviceName = state.IsAvailable
            ? state.DeviceName
            : localizer.GetText("NoMicrophone");
        IsAvailable = state.IsAvailable;
        IsMuted = state.IsMuted;
        IsActive = level >= 0.08;
    }

    private static double NormalizeLevel(double level) =>
        Math.Clamp(Math.Pow(Math.Clamp(level, 0, 1), 0.45) * 1.15, 0, 1);
}
