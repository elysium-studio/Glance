using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Clipboard.WinUI;

public sealed class ClipboardModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ClipboardShelfViewModel>();
        services.AddSingleton<IGlanceComponent, ClipboardComponent>();
    }
}
