using Glance.Transcription;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Whisper.net;

namespace Glance.Transcription.Windows;

internal sealed class WhisperTranscriptionSession :
    ITranscriptionSession
{
    private const int SampleRate = 16000;
    private const int ChunkMilliseconds = 40;
    private const int BytesPerChunk = SampleRate * sizeof(short) * ChunkMilliseconds / 1000;
    private const int SilenceChunksToComplete = 15;
    private const int PreRollChunks = 8;
    private const int PartialIntervalChunks = 60;
    private readonly WhisperFactory factory;
    private readonly WhisperProcessor processor;
    private readonly MMDevice device;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<byte[]> audio = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true
    });
    private readonly Channel<TranscriptionResult> results = Channel.CreateUnbounded<TranscriptionResult>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = true
    });
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private BufferedWaveProvider? sourceBuffer;
    private WasapiCapture? capture;
    private CancellationTokenSource? captureCancellation;
    private Task? capturePump;
    private Task? transcriptionPump;
    private bool paused;
    private int disposed;

    public WhisperTranscriptionSession(WhisperFactory factory,
        WhisperProcessor processor,
        MMDevice device)
    {
        this.factory = factory;
        this.processor = processor;
        this.device = device;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            transcriptionPump = Task.Run(() => TranscribeAsync(lifetime.Token), lifetime.Token);
            StartCapture();
        }
        finally
        {
            _ = stateGate.Release();
        }
    }

    public async IAsyncEnumerable<TranscriptionResult> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (TranscriptionResult result in results.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            if (paused || Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            paused = true;
            await StopCaptureAsync();
        }
        finally
        {
            _ = stateGate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            if (!paused || Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            paused = false;
            StartCapture();
        }
        finally
        {
            _ = stateGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await lifetime.CancelAsync();
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            await StopCaptureAsync();
            audio.Writer.TryComplete();
        }
        finally
        {
            _ = stateGate.Release();
        }

        if (transcriptionPump is not null)
        {
            await transcriptionPump.WaitAsync(cancellationToken);
        }

        results.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch (Exception)
        {
        }

        lifetime.Dispose();
        stateGate.Dispose();
        processor.Dispose();
        factory.Dispose();
        device.Dispose();
    }

    private void StartCapture()
    {
        captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        capture = new WasapiCapture(device);
        sourceBuffer = new BufferedWaveProvider(capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        ISampleProvider sampleProvider = sourceBuffer.ToSampleProvider();
        sampleProvider = sampleProvider.WaveFormat.Channels == 1
            ? sampleProvider
            : new DownmixSampleProvider(sampleProvider);
        sampleProvider = sampleProvider.WaveFormat.SampleRate == SampleRate
            ? sampleProvider
            : new WdlResamplingSampleProvider(sampleProvider, SampleRate);
        IWaveProvider waveProvider = new SampleToWaveProvider16(sampleProvider);
        capture.DataAvailable += HandleDataAvailable;
        capture.RecordingStopped += HandleRecordingStopped;
        capturePump = Task.Run(() => CaptureAsync(waveProvider, captureCancellation.Token), captureCancellation.Token);
        capture.StartRecording();
    }

    private async Task StopCaptureAsync()
    {
        WasapiCapture? currentCapture = capture;
        Task? currentPump = capturePump;
        CancellationTokenSource? currentCancellation = captureCancellation;
        capture = null;
        capturePump = null;
        captureCancellation = null;
        sourceBuffer = null;

        if (currentCancellation is not null)
        {
            await currentCancellation.CancelAsync();
        }

        if (currentCapture is not null)
        {
            currentCapture.DataAvailable -= HandleDataAvailable;
            currentCapture.RecordingStopped -= HandleRecordingStopped;

            try
            {
                currentCapture.StopRecording();
            }
            catch (Exception)
            {
            }

            currentCapture.Dispose();
        }

        if (currentPump is not null)
        {
            try
            {
                await currentPump;
            }
            catch (OperationCanceledException)
            {
            }
        }

        currentCancellation?.Dispose();
    }

    private void HandleDataAvailable(object? sender, WaveInEventArgs args)
    {
        if (args.BytesRecorded > 0)
        {
            sourceBuffer?.AddSamples(args.Buffer, 0, args.BytesRecorded);
        }
    }

    private void HandleRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is not null)
        {
            audio.Writer.TryComplete(args.Exception);
        }
    }

    private async Task CaptureAsync(IWaveProvider waveProvider,
        CancellationToken cancellationToken)
    {
        byte[] readBuffer = new byte[BytesPerChunk];
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(ChunkMilliseconds));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            int bytesRead = waveProvider.Read(readBuffer, 0, readBuffer.Length);

            if (bytesRead == 0)
            {
                continue;
            }

            byte[] chunk = new byte[bytesRead];
            Buffer.BlockCopy(readBuffer, 0, chunk, 0, bytesRead);
            await audio.Writer.WriteAsync(chunk, cancellationToken);
        }
    }

    private async Task TranscribeAsync(CancellationToken cancellationToken)
    {
        Queue<byte[]> preRoll = new();
        using MemoryStream utterance = new();
        int silenceChunks = 0;
        int chunksSincePartial = 0;
        bool speaking = false;
        long elapsedChunks = 0;
        long utteranceStartChunk = 0;
        string lastPartial = string.Empty;

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

                    preRoll.Clear();
                    silenceChunks = 0;
                    chunksSincePartial = 0;
                    continue;
                }

                utterance.Write(chunk);
                chunksSincePartial++;
                silenceChunks = hasSpeech ? 0 : silenceChunks + 1;

                if (silenceChunks >= SilenceChunksToComplete)
                {
                    await PublishAsync(utterance.ToArray(), true, utteranceStartChunk, elapsedChunks, cancellationToken);
                    utterance.SetLength(0);
                    speaking = false;
                    silenceChunks = 0;
                    chunksSincePartial = 0;
                    lastPartial = string.Empty;
                    continue;
                }

                if (chunksSincePartial >= PartialIntervalChunks)
                {
                    string partial = await RecognizeAsync(utterance.ToArray(), cancellationToken);

                    if (!string.IsNullOrWhiteSpace(partial) && !string.Equals(partial, lastPartial, StringComparison.Ordinal))
                    {
                        lastPartial = partial;
                        await results.Writer.WriteAsync(new TranscriptionResult(partial,
                            false,
                            ChunkToTime(utteranceStartChunk),
                            ChunkToTime(elapsedChunks)),
                            cancellationToken);
                    }

                    chunksSincePartial = 0;
                }
            }

            if (utterance.Length > 0)
            {
                await PublishAsync(utterance.ToArray(), true, utteranceStartChunk, elapsedChunks, cancellationToken);
            }

            results.Writer.TryComplete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            results.Writer.TryComplete();
        }
        catch (Exception exception)
        {
            results.Writer.TryComplete(exception);
        }
    }

    private async Task PublishAsync(byte[] pcm,
        bool isFinal,
        long startChunk,
        long endChunk,
        CancellationToken cancellationToken)
    {
        string text = await RecognizeAsync(pcm, cancellationToken);

        if (!string.IsNullOrWhiteSpace(text))
        {
            await results.Writer.WriteAsync(new TranscriptionResult(text,
                isFinal,
                ChunkToTime(startChunk),
                ChunkToTime(endChunk)),
                cancellationToken);
        }
    }

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

        return sampleCount > 0 && magnitude / sampleCount >= 350;
    }

    private static TimeSpan ChunkToTime(long chunk) => TimeSpan.FromMilliseconds(chunk * ChunkMilliseconds);

    private sealed class DownmixSampleProvider(ISampleProvider source) :
        ISampleProvider
    {
        private readonly ISampleProvider source = source;
        private float[] sourceBuffer = new float[4096];

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

        public int Read(float[] buffer,
            int offset,
            int count)
        {
            int sourceChannels = source.WaveFormat.Channels;
            int requiredSamples = count * sourceChannels;

            if (sourceBuffer.Length < requiredSamples)
            {
                sourceBuffer = new float[requiredSamples];
            }

            int sourceSamplesRead = source.Read(sourceBuffer, 0, requiredSamples);
            int framesRead = sourceSamplesRead / sourceChannels;

            for (int frame = 0; frame < framesRead; frame++)
            {
                float sum = 0;

                for (int channel = 0; channel < sourceChannels; channel++)
                {
                    sum += sourceBuffer[(frame * sourceChannels) + channel];
                }

                buffer[offset + frame] = sum / sourceChannels;
            }

            return framesRead;
        }
    }
}
