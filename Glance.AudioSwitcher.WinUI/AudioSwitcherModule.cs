using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.AudioSwitcher.WinUI;

public sealed class AudioSwitcherModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<AudioSwitcherModule>>();
        _ = services.AddSingleton<IAudioDeviceService, WindowsAudioDeviceService>();
        _ = services.AddSingleton(provider => new AudioSwitcherViewModel(provider.GetRequiredService<IAudioDeviceService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<AudioSwitcherModule>>()));
        _ = services.AddSingleton<AudioSwitcherComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<AudioSwitcherComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<AudioSwitcherComponent>());
    }
}
