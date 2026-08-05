using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Media.WinUI;

public sealed class MediaModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<MediaSettings>("Media", "media.settings.dat", MediaJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<MediaModule>>();
        _ = services.AddSingleton(provider => new MediaViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<MediaModule>>(), provider.GetRequiredService<GlanceModuleOptions<MediaSettings>>().Current, provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDispatcher>()));
        _ = services.AddSingleton<MediaComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<MediaComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<MediaComponent>());
        _ = services.AddSingleton<IGlanceAssistantCommandHandler, MediaAssistantCommandHandler>();
        _ = services.AddViewFor<AudioVisualizationSettingView, IGlanceModuleSettingViewModel, AudioVisualizationSettingViewModel>(ServiceLifetime.Transient, provider => new AudioVisualizationSettingView(), provider => new AudioVisualizationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<MediaSettings>>().Current, provider.GetRequiredService<IWritableOptions<MediaSettings>>()));
    }
}
