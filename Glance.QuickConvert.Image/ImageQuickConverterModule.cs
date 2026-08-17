using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.QuickConvert.Image;

public sealed class ImageQuickConverterModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ImageQuickConverterModule>>();
        _ = services.AddSingleton<IGlanceQuickConverter, ImageQuickConverter>();
    }
}
