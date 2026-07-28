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
    [NotifyPropertyChangedFor(nameof(CanZoomIn))]
    [NotifyPropertyChangedFor(nameof(CanZoomOut))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    private bool isAvailable = true;

    public bool CanClose => IsAvailable && ZoomFactor > 1;

    public bool CanZoomIn => IsAvailable && ZoomFactor < 16;

    public bool CanZoomOut => IsAvailable && ZoomFactor > 1;

    public string ZoomText => $"{ZoomFactor * 100:0}%";

    public string DetailText => !IsAvailable
        ? localizer.GetText("UnavailableDetail")
        : ZoomFactor > 1
            ? localizer.GetText("ActiveDetail")
            : localizer.GetText("InactiveDetail");

    public void Refresh()
    {
        MagnifierState state = magnifierService.GetState();
        IsAvailable = state.IsAvailable;
        ZoomFactor = Math.Clamp(state.ZoomFactor, 1, 16);
    }

    public void ZoomIn()
    {
        if (CanZoomIn)
        {
            magnifierService.ZoomIn();
        }
    }

    public void ZoomOut()
    {
        if (CanZoomOut)
        {
            magnifierService.ZoomOut();
        }
    }

    public void Close()
    {
        if (CanClose)
        {
            magnifierService.Close();
        }
    }
}
