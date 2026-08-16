using Microsoft.AI.Foundry.Local;
using Microsoft.AI.Foundry.Local.OpenAI;
using System.Runtime.CompilerServices;

namespace Glance.Transcription.Nemotron;

internal sealed class NemotronTranscriptionDecoder :
    ITranscriptionDecoder
{
    private readonly LiveAudioTranscriptionSession session;
    private int completed;
    private int disposed;

    private NemotronTranscriptionDecoder(OpenAIAudioClient client,
        string language)
    {
        session = client.CreateLiveTranscriptionSession();
        session.Settings.SampleRate = TranscriptionAudioFormat.Speech.SampleRate;
        session.Settings.Channels = TranscriptionAudioFormat.Speech.Channels;
        session.Settings.BitsPerSample = TranscriptionAudioFormat.Speech.BitsPerSample;
        session.Settings.Language = string.IsNullOrWhiteSpace(language) ? "auto" : language;
        session.Settings.PushQueueCapacity = 100;
    }

    public static async Task<NemotronTranscriptionDecoder> CreateAsync(OpenAIAudioClient client,
        string language,
        CancellationToken cancellationToken)
    {
        NemotronTranscriptionDecoder decoder = new(client, language);

        try
        {
            await decoder.session.StartAsync(cancellationToken);
            return decoder;
        }
        catch
        {
            await decoder.session.DisposeAsync();
            throw;
        }
    }

    public async Task AppendAsync(ReadOnlyMemory<byte> audio,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        await session.AppendAsync(audio, cancellationToken);
    }

    public async IAsyncEnumerable<TranscriptionResult> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (LiveAudioTranscriptionResponse result in session.GetStream(cancellationToken))
        {
            string text = result.Content?.FirstOrDefault()?.Text ??
                result.Content?.FirstOrDefault()?.Transcript ??
                string.Empty;

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return new TranscriptionResult(text,
                    result.IsFinal,
                    TimeSpan.FromSeconds(result.StartTime ?? 0),
                    TimeSpan.FromSeconds(result.EndTime ?? 0));
            }
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref completed, 1) == 0)
        {
            await session.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await CompleteAsync();
        }
        catch (Exception)
        {
        }

        await session.DisposeAsync();
    }
}
