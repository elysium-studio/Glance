using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Transcription.Nemotron;

public sealed class NemotronModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<NemotronTranscriptionProvider>();
        _ = services.AddSingleton<ITranscriptionProvider>(provider => provider.GetRequiredService<NemotronTranscriptionProvider>());
    }
}
