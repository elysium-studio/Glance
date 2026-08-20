using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.FileSystem;

public sealed class FileSystemInspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<FileSystemInspectorModule>>();
        _ = services.AddSingleton<IGlanceInspectorProvider, FileSystemInspectorProvider>();
    }
}
