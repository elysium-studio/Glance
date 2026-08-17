using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Presence;

public sealed partial class PresenceViewModel(IPresenceService presenceService,
    ITextLocalizer localizer,
    IDispatcher? dispatcher = null) :
    ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionGlyph))]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isActive = presenceService.IsActive;

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

    public string ActionGlyph => IsActive ? "\uEE95" : "\uF5B0";

    public event EventHandler? SessionStateChanged;

    public async Task RestoreAsync(bool shouldResume)
    {
        if (!shouldResume || IsActive)
        {
            SessionStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        bool succeeded;

        try
        {
            succeeded = await presenceService.SetActiveAsync(true).ConfigureAwait(false);
        }
        catch
        {
            succeeded = false;
        }

        Dispatch(() =>
        {
            if (succeeded)
            {
                IsActive = true;
            }

            SessionStateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task ToggleAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        bool requestedState = !IsActive;
        bool succeeded;

        try
        {
            succeeded = await presenceService.SetActiveAsync(requestedState).ConfigureAwait(false);
        }
        catch
        {
            succeeded = false;
        }

        Dispatch(() =>
        {
            if (succeeded)
            {
                IsActive = requestedState;
                SessionStateChanged?.Invoke(this, EventArgs.Empty);
            }

            IsBusy = false;
        });
    }

    private void Dispatch(Action action)
    {
        if (dispatcher is null)
        {
            action();
            return;
        }

        dispatcher.Dispatch(action);
    }
}
