using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace Glance.Weather.WinUI;

public sealed class WeatherModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<WeatherSettings>("Weather", "weather.settings.dat", WeatherJsonContext.Default);
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IWeatherService, OpenWeatherService>();
        services.AddSingleton<WeatherConfigurationValidator>();
        services.AddSingleton<ModuleResourceTextLocalizer<WeatherModule>>();
        services.AddSingleton(provider => new WeatherViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<WeatherModule>>()));
        services.AddSingleton<IGlanceComponent, WeatherComponent>();
        services.AddViewFor<WeatherApiKeySettingView, IGlanceModuleSettingViewModel, WeatherApiKeySettingViewModel>(ServiceLifetime.Transient, provider => new WeatherApiKeySettingView(), provider => new WeatherApiKeySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<WeatherSettings>>().Current, provider.GetRequiredService<IWritableOptions<WeatherSettings>>(), provider.GetRequiredService<WeatherConfigurationValidator>(), provider.GetRequiredService<ModuleResourceTextLocalizer<WeatherModule>>()));
        services.AddViewFor<WeatherLocationSettingView, IGlanceModuleSettingViewModel, WeatherLocationSettingViewModel>(ServiceLifetime.Transient, provider => new WeatherLocationSettingView(), provider => new WeatherLocationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<WeatherSettings>>().Current, provider.GetRequiredService<IWritableOptions<WeatherSettings>>(), provider.GetRequiredService<WeatherConfigurationValidator>(), provider.GetRequiredService<ModuleResourceTextLocalizer<WeatherModule>>()));
        services.AddViewFor<WeatherUnitsSettingView, IGlanceModuleSettingViewModel, WeatherUnitsSettingViewModel>(ServiceLifetime.Transient, provider => new WeatherUnitsSettingView(), provider => new WeatherUnitsSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<WeatherSettings>>().Current, provider.GetRequiredService<IWritableOptions<WeatherSettings>>()));
#if DEBUG
        services.AddViewFor<WeatherDebugPreviewSettingView, IGlanceModuleSettingViewModel, WeatherDebugPreviewSettingViewModel>(ServiceLifetime.Transient, provider => new WeatherDebugPreviewSettingView(), provider => new WeatherDebugPreviewSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<WeatherSettings>>().Current, provider.GetRequiredService<IWritableOptions<WeatherSettings>>()));
#endif
    }
}
