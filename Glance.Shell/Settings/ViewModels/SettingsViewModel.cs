using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;

namespace Glance.Shell;

public sealed partial class SettingsViewModel :
    ObservableCollectionViewModel<ISettingViewModel>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReorderCurrentView))]
    [NotifyPropertyChangedFor(nameof(CanStartReordering))]
    private ISettingViewModel? currentView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartReordering))]
    private bool isReorderingCurrentView;

    public SettingsViewModel(IServiceProvider provider,
        IServiceFactory factory,
        IMessenger messenger,
        IDisposer disposer,
        IEnumerable<ISettingViewModel> items) :
        base(provider, factory, messenger, disposer, items)
    {
        CurrentView = SelectedItem;
    }

    public bool CanReorderCurrentView =>
        CurrentView is IReorderableSettingViewModel { CanReorder: true };

    public bool CanStartReordering =>
        CanReorderCurrentView && !IsReorderingCurrentView;

    public void NavigateTo(ISettingViewModel? viewModel)
    {
        if (ReferenceEquals(CurrentView, viewModel))
        {
            return;
        }

        CancelReordering();
        CurrentView = viewModel;
    }

    public void BeginReordering()
    {
        if (CurrentView is not IReorderableSettingViewModel reorderable)
        {
            return;
        }

        reorderable.BeginReordering();
        IsReorderingCurrentView = reorderable.IsReordering;
    }

    public async Task CompleteReorderingAsync()
    {
        if (CurrentView is not IReorderableSettingViewModel reorderable)
        {
            return;
        }

        await reorderable.CompleteReorderingAsync();
        IsReorderingCurrentView = reorderable.IsReordering;
    }

    public void CancelReordering()
    {
        if (CurrentView is IReorderableSettingViewModel reorderable)
        {
            reorderable.CancelReordering();
        }

        IsReorderingCurrentView = false;
    }
}
