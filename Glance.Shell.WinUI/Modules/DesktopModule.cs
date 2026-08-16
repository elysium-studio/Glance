using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Elysium.Presentation.Abstractions;
using Glance.Application.Abstractions;
using Glance.Transcription;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace Glance.Shell.WinUI;

public sealed class DesktopModule :
    IModule
{
    public void Register(IServiceCollection services) => _ = services
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
            .AddSingleton<GlanceActionService>()
            .AddSingleton<IGlanceActionService>(provider => provider.GetRequiredService<GlanceActionService>())
            .AddSingleton<GlanceIntentService>()
            .AddSingleton<IGlanceIntentService>(provider => provider.GetRequiredService<GlanceIntentService>())
            .AddSingleton<GlanceQuickConverterRegistry>()
            .AddSingleton<IGlanceQuickConverterRegistry>(provider => provider.GetRequiredService<GlanceQuickConverterRegistry>())
            .AddViewFor(ServiceLifetime.Singleton,
                provider => new DesktopIslandView(provider.GetRequiredService<IMonitorLocator>(), provider.GetRequiredService<ITaskbarLocator>()),
                provider => new DesktopIslandViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<ModulePreferenceService>(), provider.GetRequiredService<IGlanceAttentionService>(), provider.GetRequiredService<IGlanceAssistantService>(), provider.GetRequiredService<IGlanceActionService>(), provider.GetRequiredService<IGlanceIntentService>(), provider.GetRequiredService<INavigator>(), provider.GetRequiredService<ILogger<DesktopIslandViewModel>>(), provider.GetRequiredService<GlanceSettings>(), provider.GetRequiredService<IWritableOptions<GlanceSettings>>()));
}
