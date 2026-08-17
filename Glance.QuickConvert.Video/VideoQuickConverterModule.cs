using Glance.Application.Abstractions;
using Glance.QuickConvert.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.QuickConvert.Video;

public sealed class VideoQuickConverterModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<VideoQuickConverterModule>>();
        _ = services.AddSingleton<QuickConvertToolProvider>();
        _ = services.AddSingleton<IGlanceQuickConverter, VideoQuickConverter>();
    }
}
