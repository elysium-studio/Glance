using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Timer.WinUI;

public sealed class TimerModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<TimerSettings>("Timer", "timer.settings.dat", TimerJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<TimerModule>>();
        services.AddSingleton(provider => new TimerViewModel(provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current));
        services.AddSingleton<IGlanceComponent, TimerComponent>();
        services
            .AddViewFor<TimerDefaultDurationSettingView, IGlanceModuleSettingViewModel, TimerDefaultDurationSettingViewModel>(ServiceLifetime.Transient, provider => new TimerDefaultDurationSettingView(), provider => new TimerDefaultDurationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current, provider.GetRequiredService<IWritableOptions<TimerSettings>>()))
            .AddViewFor<TimerAdjustmentSettingView, IGlanceModuleSettingViewModel, TimerAdjustmentSettingViewModel>(ServiceLifetime.Transient, provider => new TimerAdjustmentSettingView(), provider => new TimerAdjustmentSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current, provider.GetRequiredService<IWritableOptions<TimerSettings>>()));
    }
}
