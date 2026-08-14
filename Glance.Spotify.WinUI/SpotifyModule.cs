using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.Spotify;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace Glance.Spotify.WinUI;

public sealed class SpotifyModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<SpotifySettings>("Spotify", "spotify.settings.dat", SpotifyJsonContext.Default);
        _ = services.AddSingleton<HttpClient>();
        _ = services.AddSingleton<ModuleResourceTextLocalizer<SpotifyModule>>();
        _ = services.AddSingleton<ISpotifyCredentialStore, SpotifyCredentialStore>();
        _ = services.AddSingleton<ISpotifyLoopbackServerFactory, SpotifyLoopbackServerFactory>();
        _ = services.AddSingleton<ISpotifyBrowserLauncher, SpotifyBrowserLauncher>();
        _ = services.AddSingleton<ISpotifyAuthorizationBroker, SpotifyAuthorizationBroker>();
        _ = services.AddSingleton<SpotifyOAuthClient>();
        _ = services.AddSingleton<SpotifyConnectionService>();
        _ = services.AddSingleton<ISpotifyConnectionService>(provider => provider.GetRequiredService<SpotifyConnectionService>());
        _ = services.AddSingleton<ISpotifyAccessTokenProvider>(provider => provider.GetRequiredService<SpotifyConnectionService>());
        _ = services.AddSingleton<SpotifyApiService>();
        _ = services.AddSingleton<ISpotifyProfileService>(provider => provider.GetRequiredService<SpotifyApiService>());
        _ = services.AddSingleton<ISpotifyPlaybackService>(provider => provider.GetRequiredService<SpotifyApiService>());
        _ = services.AddSingleton<ISpotifySetupService, SpotifySetupService>();
        _ = services.AddSingleton(provider => new SpotifyViewModel(
            provider.GetRequiredService<ModuleResourceTextLocalizer<SpotifyModule>>(),
            provider.GetRequiredService<GlanceModuleOptions<SpotifySettings>>().Current,
            provider.GetRequiredService<IMessenger>(),
            provider.GetRequiredService<IDispatcher>()));
        _ = services.AddSingleton<SpotifyComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<SpotifyComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<SpotifyComponent>());
        _ = services.AddViewFor<SpotifyClientIdSettingView, IGlanceModuleSettingViewModel, SpotifyClientIdSettingViewModel>(
            ServiceLifetime.Transient,
            provider => new SpotifyClientIdSettingView(),
            provider => new SpotifyClientIdSettingViewModel(provider,
                provider.GetRequiredService<IServiceFactory>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDisposer>(),
                provider.GetRequiredService<IDispatcher>(),
                provider.GetRequiredService<GlanceModuleOptions<SpotifySettings>>().Current,
                provider.GetRequiredService<IWritableOptions<SpotifySettings>>(),
                provider.GetRequiredService<ISpotifySetupService>(),
                provider.GetRequiredService<ModuleResourceTextLocalizer<SpotifyModule>>()));
        _ = services.AddViewFor<SpotifyConnectionSettingView, IGlanceModuleSettingViewModel, SpotifyConnectionSettingViewModel>(
            ServiceLifetime.Transient,
            provider => new SpotifyConnectionSettingView(),
            provider => new SpotifyConnectionSettingViewModel(
                provider.GetRequiredService<GlanceModuleOptions<SpotifySettings>>().Current,
                provider.GetRequiredService<ISpotifyConnectionService>(),
                provider.GetRequiredService<ISpotifyProfileService>(),
                provider.GetRequiredService<ModuleResourceTextLocalizer<SpotifyModule>>(),
                provider.GetRequiredService<IMessenger>(),
                provider.GetRequiredService<IDispatcher>()));
    }
}
