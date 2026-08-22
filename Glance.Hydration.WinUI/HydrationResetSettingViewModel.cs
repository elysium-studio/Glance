using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System;
using System.ComponentModel;

namespace Glance.Hydration.WinUI;

public sealed class HydrationResetSettingViewModel :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    private readonly GlanceModuleOptions<HydrationSettings> options;
    private readonly TimeProvider timeProvider;
    private readonly HydrationViewModel viewModel;

    public HydrationResetSettingViewModel(HydrationViewModel viewModel, GlanceModuleOptions<HydrationSettings> options, TimeProvider timeProvider)
    {
        this.viewModel = viewModel;
        this.options = options;
        this.timeProvider = timeProvider;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    public string ModuleId => "Hydration";

    public int Order => 40;

    public bool CanReset => viewModel.ConsumedMillilitres > 0;

    public void Reset() => viewModel.ResetDay(options.Current, timeProvider.GetLocalNow());

    public void Dispose()
    {
        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        GC.SuppressFinalize(this);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(HydrationViewModel.ConsumedMillilitres))
        {
            OnPropertyChanged(nameof(CanReset));
        }
    }
}
