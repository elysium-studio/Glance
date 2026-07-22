using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Media.WinUI;

public sealed class MediaModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<MediaSettings>("Media", "media.settings.dat", MediaJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<MediaModule>>();
        services.AddSingleton(provider => new MediaViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<MediaModule>>(), provider.GetRequiredService<GlanceModuleOptions<MediaSettings>>().Current, provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDispatcher>()));
        services.AddSingleton<IGlanceComponent, MediaComponent>();
        services.AddViewFor<AudioVisualizationSettingView, IGlanceModuleSettingViewModel, AudioVisualizationSettingViewModel>(ServiceLifetime.Transient, provider => new AudioVisualizationSettingView(), provider => new AudioVisualizationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<MediaSettings>>().Current, provider.GetRequiredService<IWritableOptions<MediaSettings>>()));
    }
}
