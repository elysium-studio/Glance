using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Glance.Hydration.WinUI;

public sealed class HydrationModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<HydrationSettings>("Hydration", "hydration.settings.dat", HydrationJsonContext.Default);
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton<HydrationReminderPolicy>();
        _ = services.AddSingleton<ModuleResourceTextLocalizer<HydrationModule>>();
        _ = services.AddSingleton(provider => new HydrationViewModel(provider.GetRequiredService<GlanceModuleOptions<HydrationSettings>>().Current, provider.GetRequiredService<HydrationReminderPolicy>(), provider.GetRequiredService<ModuleResourceTextLocalizer<HydrationModule>>(), provider.GetRequiredService<TimeProvider>().GetLocalNow()));
        _ = services.AddSingleton<HydrationComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<HydrationComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<HydrationComponent>());
        _ = services
            .AddViewFor<HydrationGoalSettingView, IGlanceModuleSettingViewModel, HydrationGoalSettingViewModel>(ServiceLifetime.Transient, provider => new HydrationGoalSettingView(), provider => new HydrationGoalSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<HydrationSettings>>().Current, provider.GetRequiredService<IWritableOptions<HydrationSettings>>()))
            .AddViewFor<HydrationServingSettingView, IGlanceModuleSettingViewModel, HydrationServingSettingViewModel>(ServiceLifetime.Transient, provider => new HydrationServingSettingView(), provider => new HydrationServingSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<HydrationSettings>>().Current, provider.GetRequiredService<IWritableOptions<HydrationSettings>>()))
            .AddViewFor<HydrationReminderSettingsView, IGlanceModuleSettingViewModel, HydrationReminderSettingsViewModel>(ServiceLifetime.Transient, provider => new HydrationReminderSettingsView(), provider => new HydrationReminderSettingsViewModel(provider.GetRequiredService<GlanceModuleOptions<HydrationSettings>>().Current, provider.GetRequiredService<IWritableOptions<HydrationSettings>>()))
            .AddViewFor<HydrationResetSettingView, IGlanceModuleSettingViewModel, HydrationResetSettingViewModel>(ServiceLifetime.Transient, provider => new HydrationResetSettingView(), provider => new HydrationResetSettingViewModel(provider.GetRequiredService<HydrationViewModel>(), provider.GetRequiredService<GlanceModuleOptions<HydrationSettings>>(), provider.GetRequiredService<TimeProvider>()));
    }
}
