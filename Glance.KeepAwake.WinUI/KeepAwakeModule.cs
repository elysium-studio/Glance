using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.KeepAwake.WinUI;

public sealed class KeepAwakeModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<KeepAwakeSettings>("KeepAwake", "keep-awake.settings.dat", KeepAwakeJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<KeepAwakeModule>>();
        services.AddSingleton<IKeepAwakeService, WindowsKeepAwakeService>();
        services.AddSingleton(provider => new KeepAwakeViewModel(provider.GetRequiredService<IKeepAwakeService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<KeepAwakeModule>>(), provider.GetRequiredService<IDispatcher>()));
        services.AddSingleton<KeepAwakeComponent>();
        services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<KeepAwakeComponent>());
        services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<KeepAwakeComponent>());
        services.AddViewFor<KeepAwakeResumeAutomaticallySettingView, IGlanceModuleSettingViewModel, KeepAwakeResumeAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new KeepAwakeResumeAutomaticallySettingView(), provider => new KeepAwakeResumeAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<KeepAwakeSettings>>().Current, provider.GetRequiredService<IWritableOptions<KeepAwakeSettings>>()));
    }
}
