using CommunityToolkit.Mvvm.Messaging;
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
        services.AddSingleton<ThemeTransitionService>();
        services.AddSingleton<IThemeController, WindowsThemeController>();
        services.AddSingleton(provider => new ThemeSwitcherViewModel(provider.GetRequiredService<IThemeController>(), provider.GetRequiredService<GlanceModuleOptions<ThemeSwitcherSettings>>().Current, provider.GetRequiredService<ModuleResourceTextLocalizer<ThemeSwitcherModule>>()));
        services.AddSingleton<IGlanceComponent, ThemeSwitcherComponent>();
        services.AddViewFor<AnimateTransitionsSettingView, IGlanceModuleSettingViewModel, AnimateTransitionsSettingViewModel>(ServiceLifetime.Transient, provider => new AnimateTransitionsSettingView(), provider => new AnimateTransitionsSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ThemeSwitcherSettings>>().Current, provider.GetRequiredService<IWritableOptions<ThemeSwitcherSettings>>()));
    }
}
