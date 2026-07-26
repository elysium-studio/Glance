using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.KeepAwake;

public sealed partial class KeepAwakeViewModel(IKeepAwakeService keepAwakeService,
    ITextLocalizer localizer) :
    ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionGlyph))]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isActive = keepAwakeService.IsActive;

    [ObservableProperty]
    private bool isBusy;

    public string StatusText => IsActive
        ? localizer.GetText("ActiveStatus")
        : localizer.GetText("InactiveStatus");

    public string DetailText => IsActive
        ? localizer.GetText("ActiveDetail")
        : localizer.GetText("InactiveDetail");

    public string ActionLabel => IsActive
        ? localizer.GetText("StopLabel")
        : localizer.GetText("StartLabel");

    public string ActionGlyph => IsActive ? "\uE71A" : "\uE768";

    public async Task ToggleAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            bool requestedState = !IsActive;

            if (await keepAwakeService.SetActiveAsync(requestedState))
            {
                IsActive = requestedState;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
