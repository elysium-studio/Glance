using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Glance.FocusSession.WinUI;

public sealed class FocusSessionModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<FocusSessionSettings>("FocusSession", "focus-session.settings.dat", FocusSessionJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<FocusSessionModule>>();
        services.AddSingleton(provider =>
        {
            FocusSessionSettings settings = provider.GetRequiredService<GlanceModuleOptions<FocusSessionSettings>>().Current;
            return new FocusSessionViewModel(TimeSpan.FromMinutes(settings.FocusDurationMinutes), TimeSpan.FromMinutes(settings.BreakDurationMinutes));
        });
        services.AddSingleton<IGlanceComponent, FocusSessionComponent>();
        services
            .AddViewFor<FocusDurationSettingView, IGlanceModuleSettingViewModel, FocusDurationSettingViewModel>(ServiceLifetime.Transient, provider => new FocusDurationSettingView(), provider => new FocusDurationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<FocusSessionSettings>>().Current, provider.GetRequiredService<IWritableOptions<FocusSessionSettings>>()))
            .AddViewFor<BreakDurationSettingView, IGlanceModuleSettingViewModel, BreakDurationSettingViewModel>(ServiceLifetime.Transient, provider => new BreakDurationSettingView(), provider => new BreakDurationSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<FocusSessionSettings>>().Current, provider.GetRequiredService<IWritableOptions<FocusSessionSettings>>()));
    }
}
