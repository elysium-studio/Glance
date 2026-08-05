using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.PrivacyControls.WinUI;

public sealed class PrivacyControlsModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<PrivacyControlsModule>>();
        _ = services.AddSingleton<IMicrophoneService, WindowsMicrophoneService>();
        _ = services.AddSingleton(provider => new PrivacyControlsViewModel(provider.GetRequiredService<IMicrophoneService>(), provider.GetRequiredService<ModuleResourceTextLocalizer<PrivacyControlsModule>>()));
        _ = services.AddSingleton<PrivacyControlsComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<PrivacyControlsComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<PrivacyControlsComponent>());
    }
}
