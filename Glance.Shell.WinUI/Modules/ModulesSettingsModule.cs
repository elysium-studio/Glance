using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace Glance.Shell.WinUI;

public sealed class ModulesSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services.AddViewFor<ModulePreferencesView, IModulesViewModel, ModulePreferencesViewModel>(
            ServiceLifetime.Transient,
            provider => new ModulePreferencesView(),
            provider => new ModulePreferencesViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<ModulePreferenceService>(), provider.GetRequiredService<IEnumerable<IGlanceModuleSettingViewModel>>()));
    }
}
