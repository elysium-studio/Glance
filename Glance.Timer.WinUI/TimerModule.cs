using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Timer.WinUI;

public sealed class TimerModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<TimerSettings>("Timer", "timer.settings.dat", TimerJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<TimerModule>>();
        _ = services.AddSingleton(provider => new TimerViewModel(provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current));
        _ = services.AddSingleton<TimerComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<TimerComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<TimerComponent>());
        _ = services.AddSingleton<IGlanceAssistantCommandHandler, TimerAssistantCommandHandler>();
        _ = services
            .AddViewFor<TimerDefaultDurationSettingView, IGlanceModuleSettingViewModel, TimerDefaultDurationSettingViewModel>(ServiceLifetime.Transient, provider => new TimerDefaultDurationSettingView(), provider => new TimerDefaultDurationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current, provider.GetRequiredService<IWritableOptions<TimerSettings>>()))
            .AddViewFor<TimerAdjustmentSettingView, IGlanceModuleSettingViewModel, TimerAdjustmentSettingViewModel>(ServiceLifetime.Transient, provider => new TimerAdjustmentSettingView(), provider => new TimerAdjustmentSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current, provider.GetRequiredService<IWritableOptions<TimerSettings>>()))
            .AddViewFor<TimerResumeAutomaticallySettingView, IGlanceModuleSettingViewModel, TimerResumeAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new TimerResumeAutomaticallySettingView(), provider => new TimerResumeAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<TimerSettings>>().Current, provider.GetRequiredService<IWritableOptions<TimerSettings>>()));
    }
}
