using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Glance.Inspector;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Inspector.WinUI;

public sealed class InspectorModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ModuleResourceTextLocalizer<InspectorModule>>();
        _ = services.AddSingleton(provider => new InspectorViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<InspectorModule>>()));
        _ = services.AddSingleton<InspectorComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<InspectorComponent>());
        _ = services.AddSingleton<IGlanceIntent>(provider => provider.GetRequiredService<InspectorComponent>());
        _ = services.AddView<InspectorProviderRemoveDialog>(ServiceLifetime.Transient);
        _ = services.AddViewFor<InspectorProviderSettingsView, IGlanceModuleSettingViewModel, InspectorProviderSettingsViewModel>(ServiceLifetime.Transient, provider => new InspectorProviderSettingsView(), provider => new InspectorProviderSettingsViewModel(provider.GetRequiredService<IGlanceInspectorProviderManager>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<ModuleResourceTextLocalizer<InspectorModule>>(), provider.GetRequiredService<INavigator>()));
    }
}
