using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ScreenLens.WinUI;

public sealed class ScreenLensModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<ScreenLensModule>>();
        services.AddSingleton<IScreenLensService, WindowsScreenLensService>();
        services.AddSingleton(provider => new ScreenLensViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenLensModule>>()));
        services.AddSingleton<IGlanceComponent, ScreenLensComponent>();
    }
}
