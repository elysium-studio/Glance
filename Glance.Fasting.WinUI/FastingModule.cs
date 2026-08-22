using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Glance.Fasting.WinUI;

public sealed class FastingModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<FastingSettings>("Fasting", "fasting.settings.dat", FastingJsonContext.Default);
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<FastingModule>>();
        _ = services.AddSingleton(provider => new FastingViewModel(provider.GetRequiredService<GlanceModuleOptions<FastingSettings>>().Current, provider.GetRequiredService<ModuleResourceTextLocalizer<FastingModule>>(), provider.GetRequiredService<TimeProvider>().GetLocalNow()));
        _ = services.AddSingleton<FastingComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<FastingComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<FastingComponent>());
        _ = services.AddViewFor<FastingPlanSettingView, IGlanceModuleSettingViewModel, FastingPlanSettingViewModel>(ServiceLifetime.Transient, provider => new FastingPlanSettingView(), provider => new FastingPlanSettingViewModel(provider.GetRequiredService<GlanceModuleOptions<FastingSettings>>().Current, provider.GetRequiredService<IWritableOptions<FastingSettings>>()));
    }
}
