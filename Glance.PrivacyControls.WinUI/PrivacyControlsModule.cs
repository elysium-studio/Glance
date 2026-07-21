using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.PrivacyControls.WinUI;

public sealed class PrivacyControlsModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<PrivacyControlsModule>>();
        services.AddSingleton<IMicrophoneService, WindowsMicrophoneService>();
        services.AddSingleton(provider => new PrivacyControlsViewModel(provider.GetRequiredService<IMicrophoneService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<PrivacyControlsModule>>()));
        services.AddSingleton<IGlanceComponent, PrivacyControlsComponent>();
    }
}
