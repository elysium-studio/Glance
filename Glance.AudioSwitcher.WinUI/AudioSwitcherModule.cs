using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.AudioSwitcher.WinUI;

public sealed class AudioSwitcherModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<AudioSwitcherModule>>();
        services.AddSingleton<IAudioDeviceService, WindowsAudioDeviceService>();
        services.AddSingleton(provider => new AudioSwitcherViewModel(provider.GetRequiredService<IAudioDeviceService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<AudioSwitcherModule>>()));
        services.AddSingleton<IGlanceComponent, AudioSwitcherComponent>();
    }
}
