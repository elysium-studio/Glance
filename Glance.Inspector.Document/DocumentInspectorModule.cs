using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.Document;

public sealed class DocumentInspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<DocumentInspectorModule>>();
        _ = services.AddSingleton<IGlanceInspectorProvider, DocumentInspectorProvider>();
    }
}
