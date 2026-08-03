using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class GlanceSettingsModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        services
            .AddViewFor<AssistantEnabledView, IGlanceViewModel, AssistantEnabledViewModel>(ServiceLifetime.Transient,
                provider => new AssistantEnabledView(),
                provider => new AssistantEnabledViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    config => config.IsAssistantEnabled,
                    (config, enabled) => config.IsAssistantEnabled = enabled))
            .AddViewFor<AssistantProviderView, IGlanceViewModel, AssistantProviderViewModel>(ServiceLifetime.Transient,
                provider => new AssistantProviderView(),
                provider => new AssistantProviderViewModel(provider.GetRequiredService<IGlanceAssistantService>()))
            .AddViewFor<AssistantSemanticResolverView, IGlanceViewModel, AssistantSemanticResolverViewModel>(ServiceLifetime.Transient,
                provider => new AssistantSemanticResolverView(),
                provider => new AssistantSemanticResolverViewModel(provider.GetRequiredService<IGlanceAssistantSemanticResolverService>()))
            .AddViewFor<PlacementView, IGlanceViewModel, PlacementViewModel>(ServiceLifetime.Transient,
                provider => new PlacementView(),
                provider => new PlacementViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    config => (int)config.Placement,
                    (config, placement) => config.Placement = (GlancePlacement)placement))
            .AddViewFor<ExpansionModeView, IGlanceViewModel, ExpansionModeViewModel>(ServiceLifetime.Transient,
                provider => new ExpansionModeView(),
                provider => new ExpansionModeViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    config => (int)config.ExpansionMode,
                    (config, mode) => config.ExpansionMode = (GlanceExpansionMode)mode))
            .AddViewFor<AutoHideView, IGlanceViewModel, AutoHideViewModel>(ServiceLifetime.Transient,
                provider => new AutoHideView(),
                provider => new AutoHideViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    config => config.AutoHide,
                    (config, autoHide) => config.AutoHide = autoHide));
    }
}
