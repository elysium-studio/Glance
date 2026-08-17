using Glance.Application.Abstractions;
using Glance.QuickConvert.Tooling;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.QuickConvert.OnlineMedia;

public sealed class OnlineMediaQuickConverterModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<OnlineMediaQuickConverterModule>>();
        _ = services.AddSingleton<QuickConvertToolProvider>();
        _ = services.AddSingleton<IGlanceQuickConverter, OnlineMediaQuickConverter>();
    }
}
