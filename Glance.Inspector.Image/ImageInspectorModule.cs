using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.Image;

public sealed class ImageInspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ImageInspectorModule>>();
        _ = services.AddSingleton<IGlanceInspectorProvider, ImageInspectorProvider>();
    }
}
