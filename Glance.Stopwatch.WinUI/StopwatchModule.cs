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
        services.AddSingleton<IGlanceComponent, StopwatchComponent>();
        services.AddViewFor<ResumeAutomaticallySettingView, IGlanceModuleSettingViewModel, ResumeAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new ResumeAutomaticallySettingView(), provider => new ResumeAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<StopwatchSettings>>().Current, provider.GetRequiredService<IWritableOptions<StopwatchSettings>>()));
    }
}
