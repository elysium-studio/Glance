using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class ModulesSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddView<ModuleAttentionSettingView>(ServiceLifetime.Transient, provider => new ModuleAttentionSettingView())
            .AddView<ModulesDescriptionView>(ServiceLifetime.Transient, provider => new ModulesDescriptionView())
            .AddView<ModuleSettingsItemView>(ServiceLifetime.Transient, provider => new ModuleSettingsItemView());
    }
}
