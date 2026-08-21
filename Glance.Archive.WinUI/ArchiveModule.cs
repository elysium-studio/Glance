using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Archive.WinUI;

public sealed class ArchiveModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ArchiveModule>>();
        _ = services.AddSingleton(provider => new ArchiveViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ArchiveModule>>()));
        _ = services.AddSingleton<IArchiveService, ArchiveService>();
        _ = services.AddSingleton<ArchiveComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ArchiveComponent>());
        _ = services.AddSingleton<IGlanceIntent>(provider => provider.GetRequiredService<ArchiveComponent>());
    }
}
