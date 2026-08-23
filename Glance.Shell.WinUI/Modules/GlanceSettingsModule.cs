using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.Transcription;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Shell.WinUI;

public sealed class GlanceSettingsModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
            .AddViewFor<AssistantModelSetupView, IGlanceViewModel, AssistantModelSetupViewModel>(ServiceLifetime.Transient,
                provider => new AssistantModelSetupView(),
                provider => new AssistantModelSetupViewModel(provider.GetRequiredService<ITranscriptionModelCatalog>(),
                    provider.GetRequiredService<ITranscriptionModelSelection>(),
                    provider.GetRequiredService<IDispatcher>()))
            .AddViewFor<AssistantEnabledView, IGlanceViewModel, AssistantEnabledViewModel>(ServiceLifetime.Transient,
                provider => new AssistantEnabledView(),
                provider => new AssistantEnabledViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    provider.GetRequiredService<IGlanceAssistantService>(),
                    config => config.IsAssistantEnabled,
                    (config, enabled) => config.IsAssistantEnabled = enabled))
            .AddViewFor<DisplayLocationView, IGlanceViewModel, DisplayLocationViewModel>(ServiceLifetime.Transient,
                provider => new DisplayLocationView(),
                provider => new DisplayLocationViewModel(provider,
                    provider.GetRequiredService<IServiceFactory>(),
                    provider.GetRequiredService<IMessenger>(),
                    provider.GetRequiredService<IDisposer>(),
                    provider.GetRequiredService<IDispatcher>(),
                    provider.GetRequiredService<GlanceSettings>(),
                    provider.GetRequiredService<IWritableOptions<GlanceSettings>>(),
                    config => (int)config.DisplayLocation,
                    (config, location) => config.DisplayLocation = (GlanceDisplayLocation)location))
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
