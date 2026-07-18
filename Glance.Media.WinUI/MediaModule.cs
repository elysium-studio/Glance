using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Media.WinUI;

public sealed class MediaModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<MediaViewModel>();
        services.AddSingleton<IGlanceComponent, MediaComponent>();
    }
}
