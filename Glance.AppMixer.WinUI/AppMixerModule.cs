using Glance.Application.Abstractions;
using Glance.AppMixer;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.AppMixer.WinUI;

public sealed class AppMixerModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<AppMixerModule>>();
        _ = services.AddSingleton<IAudioApplicationService, WindowsAudioApplicationService>();
        _ = services.AddSingleton(provider => new AppMixerViewModel(provider.GetRequiredService<IAudioApplicationService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<AppMixerModule>>()));
        _ = services.AddSingleton<AppMixerComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<AppMixerComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<AppMixerComponent>());
    }
}
