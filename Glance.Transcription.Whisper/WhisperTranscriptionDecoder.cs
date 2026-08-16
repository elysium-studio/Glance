using NAudio.Wave;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Whisper.net;

namespace Glance.Transcription.Whisper;

internal sealed class WhisperTranscriptionDecoder :
    ITranscriptionDecoder
{
    private const int SampleRate = 16000;
    private const int ChunkMilliseconds = 40;
    private const int AudioBufferCapacity = 150;
    private const int RecognitionBufferCapacity = 3;
    private const int SilenceChunksToComplete = 15;
    private const int PreRollChunks = 8;
    private const int PartialIntervalChunks = 12;
    private const int MaximumSegmentChunks = 150;
    private const int MinimumSpeechLevel = 80;
    private readonly WhisperFactory factory;
    private readonly WhisperProcessor processor;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<byte[]> audio = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(AudioBufferCapacity)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly Channel<TranscriptionWorkItem> recognition = Channel.CreateBounded<TranscriptionWorkItem>(new BoundedChannelOptions(RecognitionBufferCapacity)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.DropOldest
    });
    private readonly Channel<TranscriptionResult> results = Channel.CreateUnbounded<TranscriptionResult>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = true
    });
    private readonly Task transcriptionPump;
    private int completed;
    private int disposed;

    public WhisperTranscriptionDecoder(WhisperFactory factory,
        WhisperProcessor processor)
    {
        this.factory = factory;
        this.processor = processor;
        transcriptionPump = Task.Run(() => TranscribeAsync(lifetime.Token));
    }

    public Task AppendAsync(ReadOnlyMemory<byte> audio,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        _ = this.audio.Writer.TryWrite(audio.ToArray());
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<TranscriptionResult> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (TranscriptionResult result in results.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref completed, 1) == 0)
        {
            audio.Writer.TryComplete();
        }

        await transcriptionPump.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        audio.Writer.TryComplete();

        try
        {
            await transcriptionPump;
        }
        catch (Exception)
        {
        }

        await lifetime.CancelAsync();
        lifetime.Dispose();
        processor.Dispose();
        factory.Dispose();
    }

    private async Task TranscribeAsync(CancellationToken cancellationToken)
    {
        Task recognitionPump = Task.Run(() => RecognizeWorkAsync(cancellationToken), cancellationToken);

        try
        {
            await SegmentAudioAsync(cancellationToken);
        }
        finally
        {
            recognition.Writer.TryComplete();
        }

        try
        {
            await recognitionPump;
        }
        finally
        {
            results.Writer.TryComplete();
        }
    }

    private async Task SegmentAudioAsync(CancellationToken cancellationToken)
    {
        Queue<byte[]> preRoll = new();
        using MemoryStream utterance = new();
        int silenceChunks = 0;
        int chunksSincePartial = 0;
        int segmentChunks = 0;
        bool speaking = false;
        long elapsedChunks = 0;
        long utteranceStartChunk = 0;

        try
        {
            await foreach (byte[] chunk in audio.Reader.ReadAllAsync(cancellationToken))
            {
                elapsedChunks++;
                bool hasSpeech = HasSpeechLikeLevel(chunk);

                if (!speaking)
                {
                    preRoll.Enqueue(chunk);

                    while (preRoll.Count > PreRollChunks)
                    {
                        _ = preRoll.Dequeue();
                    }

                    if (!hasSpeech)
                    {
                        continue;
                    }

                    speaking = true;
                    utteranceStartChunk = Math.Max(0, elapsedChunks - preRoll.Count);

                    foreach (byte[] bufferedChunk in preRoll)
                    {
                        utterance.Write(bufferedChunk);
                    }

                    segmentChunks = preRoll.Count;
                    preRoll.Clear();
                    silenceChunks = 0;
                    chunksSincePartial = 0;
                    continue;
                }

                utterance.Write(chunk);
                segmentChunks++;
                chunksSincePartial++;
                silenceChunks = hasSpeech ? 0 : silenceChunks + 1;

                if (silenceChunks >= SilenceChunksToComplete || segmentChunks >= MaximumSegmentChunks)
                {
                    QueueRecognition(utterance, true, utteranceStartChunk, elapsedChunks);
                    utterance.SetLength(0);
                    speaking = false;
                    silenceChunks = 0;
                    chunksSincePartial = 0;
                    segmentChunks = 0;
                    continue;
                }

                if (chunksSincePartial >= PartialIntervalChunks)
                {
                    QueueRecognition(utterance, false, utteranceStartChunk, elapsedChunks);
                    chunksSincePartial = 0;
                }
            }

            if (utterance.Length > 0)
            {
                QueueRecognition(utterance, true, utteranceStartChunk, elapsedChunks);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            recognition.Writer.TryComplete(exception);
        }
    }

    private async Task RecognizeWorkAsync(CancellationToken cancellationToken)
    {
        string lastPartial = string.Empty;

        try
        {
            await foreach (TranscriptionWorkItem workItem in recognition.Reader.ReadAllAsync(cancellationToken))
            {
                string text = await RecognizeAsync(workItem.Pcm, cancellationToken);

                if (string.IsNullOrWhiteSpace(text) || !workItem.IsFinal && string.Equals(text, lastPartial, StringComparison.Ordinal))
                {
                    continue;
                }

                lastPartial = workItem.IsFinal ? string.Empty : text;
                await results.Writer.WriteAsync(new TranscriptionResult(text,
                    workItem.IsFinal,
                    ChunkToTime(workItem.StartChunk),
                    ChunkToTime(workItem.EndChunk)),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            results.Writer.TryComplete(exception);
        }
    }

    private void QueueRecognition(MemoryStream utterance,
        bool isFinal,
        long startChunk,
        long endChunk) => _ = recognition.Writer.TryWrite(new TranscriptionWorkItem(utterance.ToArray(), isFinal, startChunk, endChunk));

    private async Task<string> RecognizeAsync(byte[] pcm,
        CancellationToken cancellationToken)
    {
        await using MemoryStream wave = new();
        WaveFileWriter.WriteWavFileToStream(wave,
            new RawSourceWaveStream(new MemoryStream(pcm, false), new WaveFormat(SampleRate, 16, 1)));
        wave.Position = 0;
        List<string> segments = [];

        await foreach (SegmentData segment in processor.ProcessAsync(wave, cancellationToken))
        {
            string text = segment.Text.Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                segments.Add(text);
            }
        }

        return string.Join(' ', segments);
    }

    private static bool HasSpeechLikeLevel(byte[] buffer)
    {
        long magnitude = 0;
        int sampleCount = buffer.Length / sizeof(short);

        for (int index = 0; index < buffer.Length - 1; index += sizeof(short))
        {
            short sample = (short)(buffer[index] | buffer[index + 1] << 8);
            magnitude += Math.Abs((int)sample);
        }

        return sampleCount > 0 && magnitude / sampleCount >= MinimumSpeechLevel;
    }

    private static TimeSpan ChunkToTime(long chunk) => TimeSpan.FromMilliseconds(chunk * ChunkMilliseconds);

    private sealed record TranscriptionWorkItem(byte[] Pcm,
        bool IsFinal,
        long StartChunk,
        long EndChunk);
}
