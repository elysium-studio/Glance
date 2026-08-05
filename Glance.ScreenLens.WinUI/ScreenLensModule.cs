using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ScreenLens.WinUI;

public sealed class ScreenLensModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ScreenLensModule>>();
        _ = services.AddSingleton<IScreenLensService, WindowsScreenLensService>();
        _ = services.AddSingleton(provider => new ScreenLensViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenLensModule>>()));
        _ = services.AddSingleton<ScreenLensComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ScreenLensComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ScreenLensComponent>());
    }
}
