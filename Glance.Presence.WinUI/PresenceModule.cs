using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Glance.Presence.WinUI;

public sealed class PresenceModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<PresenceSettings>("Presence", "presence.settings.dat", PresenceJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<PresenceModule>>();
        _ = services.AddSingleton(new PresenceActivityPolicy(TimeSpan.FromMinutes(4)));
        _ = services.AddSingleton<IPresenceService, WindowsPresenceService>();
        _ = services.AddSingleton(provider => new PresenceViewModel(provider.GetRequiredService<IPresenceService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<PresenceModule>>(), provider.GetRequiredService<IDispatcher>()));
        _ = services.AddSingleton<PresenceComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<PresenceComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<PresenceComponent>());
        _ = services.AddViewFor<PresenceResumeAutomaticallySettingView, IGlanceModuleSettingViewModel, PresenceResumeAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new PresenceResumeAutomaticallySettingView(), provider => new PresenceResumeAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<PresenceSettings>>().Current, provider.GetRequiredService<IWritableOptions<PresenceSettings>>()));
    }
}
