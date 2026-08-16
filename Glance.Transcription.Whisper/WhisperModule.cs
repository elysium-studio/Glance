using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Transcription.Whisper;

public sealed class WhisperModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton(provider => new WhisperModelCatalog(
            provider.GetRequiredService<IBackgroundDownloadManager>(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Glance",
                "Transcription",
                "Models",
                "Whisper")));
        _ = services.AddSingleton<WhisperTranscriptionProvider>();
        _ = services.AddSingleton<ITranscriptionProvider>(provider => provider.GetRequiredService<WhisperTranscriptionProvider>());
    }
}
