using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.Application;

public sealed class ApplicationInspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ApplicationInspectorModule>>();
        _ = services.AddSingleton<IGlanceInspectorProvider, ApplicationInspectorProvider>();
    }
}
