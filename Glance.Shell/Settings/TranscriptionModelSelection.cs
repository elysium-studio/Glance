using Elysium.Application.Abstractions;
using Glance.Transcription;

namespace Glance.Shell;

public sealed class TranscriptionModelSelection(GlanceSettings settings,
    IWritableOptions<GlanceSettings> writer) :
    ITranscriptionModelSelection
{
    private readonly GlanceSettings settings = settings;
    private readonly IWritableOptions<GlanceSettings> writer = writer;

    public event EventHandler? SelectionChanged;

    public string? SelectedModelId => settings.TranscriptionModelId;

    public async Task SelectAsync(string modelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (string.Equals(settings.TranscriptionModelId, modelId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        settings.TranscriptionModelId = modelId;
        await writer.WriteAsync(value => value.TranscriptionModelId = modelId, cancellationToken);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
