using System;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Presence.WinUI;

public sealed class PresenceModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<PresenceModule>>();
        services.AddSingleton(new PresenceActivityPolicy(TimeSpan.FromMinutes(4)));
        services.AddSingleton<IPresenceService, WindowsPresenceService>();
        services.AddSingleton(provider => new PresenceViewModel(provider.GetRequiredService<IPresenceService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<PresenceModule>>(), provider.GetRequiredService<IDispatcher>()));
        services.AddSingleton<IGlanceComponent, PresenceComponent>();
    }
}
