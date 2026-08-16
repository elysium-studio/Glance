namespace Glance.Transcription;

public interface ITranscriptionModelSelection
{
    event EventHandler? SelectionChanged;

    string? SelectedModelId { get; }

    Task SelectAsync(string modelId,
        CancellationToken cancellationToken = default);
}
