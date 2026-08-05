using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.Magnifier;

public sealed partial class MagnifierViewModel(IMagnifierService magnifierService,
    ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    [NotifyPropertyChangedFor(nameof(CanZoomOut))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    private double zoomFactor = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanZoomIn))]
    [NotifyPropertyChangedFor(nameof(CanZoomOut))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanZoomIn))]
    [NotifyPropertyChangedFor(nameof(CanZoomOut))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private bool isAvailable = true;

    public bool CanClose => IsAvailable && IsRunning;

    public bool CanStart => IsAvailable && !IsRunning;

    public bool CanZoomIn => IsAvailable && IsRunning && ZoomFactor < 16;

    public bool CanZoomOut => IsAvailable && IsRunning && ZoomFactor > 1;

    public string ZoomText => $"{ZoomFactor * 100:0}%";

    public string DetailText => !IsAvailable
        ? localizer.GetText("UnavailableDetail")
        : IsRunning
            ? localizer.GetText("ActiveDetail")
            : localizer.GetText("InactiveDetail");

    public void Refresh()
    {
        MagnifierState state = magnifierService.GetState();
        IsAvailable = state.IsAvailable;
        IsRunning = state.IsRunning;
        ZoomFactor = Math.Clamp(state.ZoomFactor, 1, 16);
    }

    public void Start()
    {
        if (CanStart)
        {
            _ = magnifierService.Start();
        }
    }

    public void ZoomIn()
    {
        if (CanZoomIn)
        {
            _ = magnifierService.ZoomIn();
        }
    }

    public void ZoomOut()
    {
        if (CanZoomOut)
        {
            _ = magnifierService.ZoomOut();
        }
    }

    public void Close()
    {
        if (CanClose)
        {
            _ = magnifierService.Close();
        }
    }
}
