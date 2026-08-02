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
        services.AddModuleOptions<ThemeSwitcherSettings>("ThemeSwitcher", "theme-switcher.settings.dat", ThemeSwitcherJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<ThemeSwitcherModule>>();
        services.AddSingleton<WindowsSystemThemeService>();
        services.AddSingleton<WindowsLocationService>();
        services.AddSingleton<IThemeController, WindowsThemeController>();
        services.AddSingleton(provider => new ThemeSwitcherViewModel(provider.GetRequiredService<IThemeController>(), provider.GetRequiredService<GlanceModuleOptions<ThemeSwitcherSettings>>().Current, provider.GetRequiredService<ModuleResourceTextLocalizer<ThemeSwitcherModule>>(), provider.GetRequiredService<IDispatcher>()));
        services.AddSingleton<ThemeSwitcherComponent>();
        services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ThemeSwitcherComponent>());
        services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ThemeSwitcherComponent>());
    }
}
