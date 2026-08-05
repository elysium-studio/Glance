using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.QuickConvert.WinUI;

public sealed class QuickConvertModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<QuickConvertModule>>();
        _ = services.AddSingleton(provider => new QuickConvertViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<QuickConvertModule>>()));
        _ = services.AddSingleton<IGlanceQuickConverter, ImageQuickConverter>();
        _ = services.AddSingleton<IGlanceQuickConverter, VideoQuickConverter>();
        _ = services.AddSingleton<QuickConvertComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<QuickConvertComponent>());
        _ = services.AddSingleton<IGlanceIntent>(provider => provider.GetRequiredService<QuickConvertComponent>());
    }
}
