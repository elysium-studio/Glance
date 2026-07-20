using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Media.WinUI;

public sealed class MediaModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<MediaModule>>();
        services.AddSingleton(provider => new MediaViewModel(
            provider.GetRequiredService<ModuleResourceTextLocalizer<MediaModule>>()));
        services.AddSingleton<IGlanceComponent, MediaComponent>();
    }
}
