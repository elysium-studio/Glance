using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class ModulesSettingsModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
            .AddSingleton<IApplicationRestartService, WindowsApplicationRestartService>()
            .AddView<ModuleAttentionSettingView>(ServiceLifetime.Transient, provider => new ModuleAttentionSettingView())
            .AddView<ModuleSettingsItemView>(ServiceLifetime.Transient, provider => new ModuleSettingsItemView());
}
