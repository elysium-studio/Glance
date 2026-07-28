using System;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Presence.WinUI;

public sealed class PresenceModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<PresenceSettings>("Presence", "presence.settings.dat", PresenceJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<PresenceModule>>();
        services.AddSingleton(new PresenceActivityPolicy(TimeSpan.FromMinutes(4)));
        services.AddSingleton<IPresenceService, WindowsPresenceService>();
        services.AddSingleton(provider => new PresenceViewModel(provider.GetRequiredService<IPresenceService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<PresenceModule>>(), provider.GetRequiredService<IDispatcher>()));
        services.AddSingleton<IGlanceComponent, PresenceComponent>();
        services.AddViewFor<PresenceResumeAutomaticallySettingView, IGlanceModuleSettingViewModel, PresenceResumeAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new PresenceResumeAutomaticallySettingView(), provider => new PresenceResumeAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<PresenceSettings>>().Current, provider.GetRequiredService<IWritableOptions<PresenceSettings>>()));
    }
}
