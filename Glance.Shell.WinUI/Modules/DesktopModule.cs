using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Glance.Transcription;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;

namespace Glance.Shell.WinUI;

public sealed class DesktopModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
#if DEBUG
        const bool isStableFeedEnabled = false;
#else
        const bool isStableFeedEnabled = true;
#endif

        _ = services
            .AddSingleton<IGlanceAttentionService, GlanceAttentionService>()
            .AddSingleton<GlanceAssistantSemanticResolverService>()
            .AddSingleton<IGlanceAssistantSemanticResolverService>(provider => provider.GetRequiredService<GlanceAssistantSemanticResolverService>())
            .AddSingleton<GlanceAssistantCommandService>()
            .AddSingleton<IGlanceAssistantCommandService>(provider => provider.GetRequiredService<GlanceAssistantCommandService>())
            .AddSingleton<HttpClient>()
            .AddSingleton<BackgroundDownloadManager>()
            .AddSingleton<IBackgroundDownloadManager>(provider => provider.GetRequiredService<BackgroundDownloadManager>())
            .AddSingleton<TranscriptionService>()
            .AddSingleton<ITranscriptionModelCatalog>(provider => provider.GetRequiredService<TranscriptionService>())
            .AddSingleton<ITranscriptionDecoderFactory>(provider => provider.GetRequiredService<TranscriptionService>())
            .AddSingleton<ITranscriptionProviderRegistry>(provider => provider.GetRequiredService<TranscriptionService>())
            .AddSingleton<TranscriptionModelSelection>()
            .AddSingleton<ITranscriptionModelSelection>(provider => provider.GetRequiredService<TranscriptionModelSelection>())
            .AddSingleton(provider => new GlanceAssistantService(provider.GetRequiredService<GlanceSettings>(), provider.GetRequiredService<IWritableOptions<GlanceSettings>>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<IGlanceActionService>(), provider.GetRequiredService<ITranscriptionModelCatalog>(), provider.GetRequiredService<ILogger<GlanceAssistantService>>()))
            .AddSingleton<IGlanceAssistantService>(provider => provider.GetRequiredService<GlanceAssistantService>())
            .AddSingleton<IGlanceAssistantCommandHandler, ShowComponentAssistantCommandHandler>()
            .AddSingleton<ModulePreferenceService>()
            .AddSingleton<ModuleInstallationService>()
            .AddSingleton(new GlanceModuleFeedDefinition("elysium-stable", "Elysium Studio", new Uri("https://elysiumstud.io/feeds/glance/modules/stable/index.json"), isStableFeedEnabled, false, 100))
            .AddSingleton<IGlanceModuleFeedValidator, GlanceModuleFeedValidator>()
            .AddSingleton<IGlanceModuleFeedClient, GlanceModuleFeedClient>()
            .AddSingleton<IGlanceModuleFeedCache, GlanceModuleFeedCache>()
            .AddSingleton<IGlanceModuleFeedSourceProvider, GlanceModuleFeedSourceProvider>()
            .AddSingleton<IGlanceModuleFeedService, GlanceModuleFeedService>()
            .AddSingleton<IGlanceModuleDependencyResolver, GlanceModuleDependencyResolver>()
            .AddSingleton<IGlanceModulePackageService, GlanceModulePackageService>()
            .AddSingleton<GlanceActionService>()
            .AddSingleton<IGlanceActionService>(provider => provider.GetRequiredService<GlanceActionService>())
            .AddSingleton<GlanceIntentService>()
            .AddSingleton<IGlanceIntentService>(provider => provider.GetRequiredService<GlanceIntentService>())
            .AddSingleton<IGlanceQuickConverterPreferences, GlanceQuickConverterPreferences>()
            .AddSingleton<GlanceQuickConverterRegistry>()
            .AddSingleton<IGlanceQuickConverterRegistry>(provider => provider.GetRequiredService<GlanceQuickConverterRegistry>())
            .AddSingleton<GlanceQuickConverterManager>()
            .AddSingleton<IGlanceQuickConverterManager>(provider => provider.GetRequiredService<GlanceQuickConverterManager>())
            .AddSingleton<IGlanceInspectorProviderPreferences, GlanceInspectorProviderPreferences>()
            .AddSingleton<GlanceInspectorProviderRegistry>()
            .AddSingleton<IGlanceInspectorProviderRegistry>(provider => provider.GetRequiredService<GlanceInspectorProviderRegistry>())
            .AddSingleton<GlanceInspectorProviderManager>()
            .AddSingleton<IGlanceInspectorProviderManager>(provider => provider.GetRequiredService<GlanceInspectorProviderManager>())
            .AddSingleton<IDesktopIslandAnimationController, DesktopIslandAnimationController>()
            .AddSingleton<IDesktopIslandBindings, DesktopIslandBindings>()
            .AddSingleton<IDesktopIslandComponentController, DesktopIslandComponentController>()
            .AddSingleton<IDesktopIslandContentReader, DesktopIslandContentReader>()
            .AddSingleton<IDesktopIslandDisplayController, DesktopIslandDisplayController>()
            .AddSingleton<IDesktopIslandDropController, DesktopIslandDropController>()
            .AddSingleton<IDesktopIslandModuleReorderController, DesktopIslandModuleReorderController>()
            .AddSingleton<IDesktopIslandPresentationController, DesktopIslandPresentationController>()
            .AddSingleton<IDesktopIslandScreenTargetProvider, DesktopIslandScreenTargetProvider>()
            .AddViewFor(ServiceLifetime.Singleton, provider => new DesktopIslandView(provider.GetRequiredService<IDesktopIslandAnimationController>(), provider.GetRequiredService<IDesktopIslandComponentController>(), provider.GetRequiredService<IDesktopIslandDisplayController>(), provider.GetRequiredService<IDesktopIslandDropController>(), provider.GetRequiredService<IDesktopIslandModuleReorderController>(), provider.GetRequiredService<IDesktopIslandPresentationController>(), provider.GetRequiredService<IDesktopIslandScreenTargetProvider>(), provider.GetRequiredService<IDesktopIslandBindings>()), provider => new DesktopIslandViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<ModulePreferenceService>(), provider.GetRequiredService<IGlanceAttentionService>(), provider.GetRequiredService<IGlanceAssistantService>(), provider.GetRequiredService<IGlanceActionService>(), provider.GetRequiredService<IGlanceIntentService>(), provider.GetRequiredService<INavigator>(), provider.GetRequiredService<ILogger<DesktopIslandViewModel>>(), provider.GetRequiredService<GlanceSettings>(), provider.GetRequiredService<IWritableOptions<GlanceSettings>>()));

#if DEBUG
        _ = services.AddSingleton(new GlanceModuleFeedDefinition("local-solution", "Local solution", new Uri(Path.Combine(AppContext.BaseDirectory, "module-feed.json")), true, true, 0));
#endif
    }
}
