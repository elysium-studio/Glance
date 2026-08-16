using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Glance.Transcription.Windows;

public static class TranscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsTranscription(this IServiceCollection services)
    {
        services.TryAddSingleton<IAudioInputSourceCatalog, WindowsAudioInputSourceCatalog>();
        services.TryAddSingleton<ITranscriptionSessionFactory, WindowsTranscriptionSessionFactory>();
        return services;
    }
}
