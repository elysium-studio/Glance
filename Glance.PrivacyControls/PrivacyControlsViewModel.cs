using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.PrivacyControls;

public sealed partial class PrivacyControlsViewModel :
    ObservableObject
{
    private readonly ICameraUsageService cameraUsageService;
    private readonly IMicrophoneService microphoneService;
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private string deviceName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(IsMicrophoneLive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(IsMicrophoneLive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isMuted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    private bool isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    private bool isCameraActive;

    public PrivacyControlsViewModel(IMicrophoneService microphoneService,
        ICameraUsageService cameraUsageService,
        ITextLocalizer localizer)
    {
        this.microphoneService = microphoneService;
        this.cameraUsageService = cameraUsageService;
        this.localizer = localizer;
        deviceName = localizer.GetText("NoMicrophone");

        Refresh();
    }

    public string StatusText => (IsActive, IsCameraActive) switch
    {
        (true, true) => localizer.GetText("MicrophoneAndCameraActive"),
        (false, true) => localizer.GetText("CameraActive"),
        (true, false) => localizer.GetText("MicrophoneActive"),
        _ when !IsAvailable => localizer.GetText("NoMicrophone"),
        _ when IsMuted => localizer.GetText("MicrophoneMuted"),
        _ => localizer.GetText("MicrophoneReady")
    };

    public string StatusGlyph => IsCameraActive ? "\uE722" : "\uE720";

    public string ToggleGlyph => IsMuted ? "\uF5B0" : "\uF8AE";

    public bool IsMicrophoneLive => IsAvailable && !IsMuted;

    public void Refresh() => Update(microphoneService.GetState(), cameraUsageService.IsInUse());

    public void ToggleMute()
    {
        if (IsAvailable && microphoneService.TrySetMuted(!IsMuted))
        {
            Refresh();
        }
    }

    public void Update(MicrophoneState state,
        bool isCameraInUse)
    {
        DeviceName = state.IsAvailable
            ? state.DeviceName
            : localizer.GetText("NoMicrophone");
        IsAvailable = state.IsAvailable;
        IsMuted = state.IsMuted;
        IsActive = state.IsAvailable && state.IsInUse;
        IsCameraActive = isCameraInUse;
    }
}
