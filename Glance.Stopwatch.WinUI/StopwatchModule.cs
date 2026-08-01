using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Stopwatch.WinUI;

public sealed class StopwatchModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<StopwatchSettings>("Stopwatch", "stopwatch.settings.dat", StopwatchJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<StopwatchModule>>();
        services.AddSingleton(provider => new StopwatchViewModel(provider.GetRequiredService<GlanceModuleOptions<StopwatchSettings>>().Current));
        services.AddSingleton<StopwatchComponent>();
        services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<StopwatchComponent>());
        services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<StopwatchComponent>());
        services.AddViewFor<StopwatchResumeAutomaticallySettingView, IGlanceModuleSettingViewModel, StopwatchResumeAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new StopwatchResumeAutomaticallySettingView(), provider => new StopwatchResumeAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<StopwatchSettings>>().Current, provider.GetRequiredService<IWritableOptions<StopwatchSettings>>()));
    }
}
