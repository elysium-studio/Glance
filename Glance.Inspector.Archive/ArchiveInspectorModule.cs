using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.Archive;

public sealed class ArchiveInspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ArchiveInspectorModule>>();
        _ = services.AddSingleton<IGlanceInspectorProvider, ArchiveInspectorProvider>();
    }
}
