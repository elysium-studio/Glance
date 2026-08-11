using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class ModulesSettingsModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
            .AddView<ModuleAttentionSettingView>(ServiceLifetime.Transient, provider => new ModuleAttentionSettingView())
            .AddView<ModuleSettingsItemView>(ServiceLifetime.Transient,
                provider => new ModuleSettingsItemView(provider.GetRequiredService<ITextLocalizer>()));
}
