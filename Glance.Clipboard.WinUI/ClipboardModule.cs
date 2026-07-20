using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Clipboard.WinUI;

public sealed class ClipboardModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<ClipboardModule>>();
        services.AddSingleton(provider => new ClipboardShelfViewModel(
            provider.GetRequiredService<ModuleResourceTextLocalizer<ClipboardModule>>()));
        services.AddSingleton<IGlanceComponent, ClipboardComponent>();
    }
}
