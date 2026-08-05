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
        _ = services.AddSingleton<ModuleResourceTextLocalizer<StashModule>>();
        _ = services.AddSingleton<StashTextCopyService>();
        _ = services.AddSingleton<StashTextViewerService>();
        _ = services.AddSingleton(new StashRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Stash", "stash.db")));
        _ = services.AddSingleton(provider => new StashViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<StashModule>>()));
        _ = services.AddSingleton<StashComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<StashComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<StashComponent>());
        _ = services.AddSingleton<IGlanceIntent>(provider => provider.GetRequiredService<StashComponent>());
    }
}
