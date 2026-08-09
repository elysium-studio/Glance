using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Glance.WorldClock.WinUI;

public sealed class WorldClockModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<WorldClockSettings>("WorldClock", "world-clock.settings.dat", WorldClockJsonContext.Default);
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<WorldClockModule>>();
        _ = services.AddSingleton(CreateViewModel);
        _ = services.AddSingleton<WorldClockComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<WorldClockComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<WorldClockComponent>());
        _ = services.AddSingleton<IGlanceAssistantCommandHandler, WorldClockAssistantCommandHandler>();
        _ = services.AddViewFor<AddWorldClockDialog, AddWorldClockDialogViewModel>(ServiceLifetime.Transient, provider => new AddWorldClockDialog());
        _ = services.AddViewFor<WorldClockTimeFormatSettingView, IGlanceModuleSettingViewModel, WorldClockTimeFormatSettingViewModel>(ServiceLifetime.Transient, provider => new WorldClockTimeFormatSettingView(), provider => new WorldClockTimeFormatSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<WorldClockSettings>>().Current, provider.GetRequiredService<IWritableOptions<WorldClockSettings>>()));
        _ = services.AddViewFor<WorldClockLocationsSettingView, IGlanceModuleSettingViewModel, WorldClockLocationsSettingViewModel>(ServiceLifetime.Transient, provider => new WorldClockLocationsSettingView(), provider => new WorldClockLocationsSettingViewModel(provider.GetRequiredService<GlanceModuleOptions<WorldClockSettings>>().Current, provider.GetRequiredService<IWritableOptions<WorldClockSettings>>(), provider.GetRequiredService<INavigator>()));
    }

    private static WorldClockViewModel CreateViewModel(IServiceProvider provider)
    {
        ModuleResourceTextLocalizer<WorldClockModule> localizer = provider.GetRequiredService<ModuleResourceTextLocalizer<WorldClockModule>>();
        WorldClockSettings settings = provider.GetRequiredService<GlanceModuleOptions<WorldClockSettings>>().Current;
        WorldClockViewModel viewModel = new(WorldClockTimeZoneCatalog.CreateDefinitions(settings, localizer));
        viewModel.Initialize();
        return viewModel;
    }
}
