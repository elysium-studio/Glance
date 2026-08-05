using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ThemeSwitcher.WinUI;

public sealed class ThemeSwitcherModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<ThemeSwitcherSettings>("ThemeSwitcher", "theme-switcher.settings.dat", ThemeSwitcherJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ThemeSwitcherModule>>();
        _ = services.AddSingleton<WindowsSystemThemeService>();
        _ = services.AddSingleton<WindowsLocationService>();
        _ = services.AddSingleton<IThemeController, WindowsThemeController>();
        _ = services.AddSingleton(provider => new ThemeSwitcherViewModel(provider.GetRequiredService<IThemeController>(), provider.GetRequiredService<GlanceModuleOptions<ThemeSwitcherSettings>>().Current, provider.GetRequiredService<ModuleResourceTextLocalizer<ThemeSwitcherModule>>(), provider.GetRequiredService<IDispatcher>()));
        _ = services.AddSingleton<ThemeSwitcherComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ThemeSwitcherComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ThemeSwitcherComponent>());
    }
}
