using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Glance.Shell.WinUI;

public sealed class SetupTourModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
            .AddViewFor(ServiceLifetime.Transient,
                provider => new SetupTourWindow(),
                provider => new SetupTourViewModel(provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<ModulePreferenceService>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    provider.GetRequiredService<IGlanceModuleCategoryResolver>(),
                    provider.GetRequiredService<ILogger<SetupTourViewModel>>()));
}
