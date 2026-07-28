using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Glance.Stash.WinUI;

public sealed class StashModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<StashModule>>();
        services.AddSingleton<StashTextCopyService>();
        services.AddSingleton(new StashRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Stash", "stash.db")));
        services.AddSingleton(provider => new StashViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<StashModule>>()));
        services.AddSingleton<IGlanceComponent, StashComponent>();
    }
}
