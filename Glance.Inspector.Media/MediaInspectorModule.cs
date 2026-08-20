using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.Media;

public sealed class MediaInspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<MediaInspectorModule>>();
        _ = services.AddSingleton<IGlanceInspectorProvider, MediaInspectorProvider>();
    }
}
