using Glance.Transcription;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.CompilerServices;

namespace Glance.Transcription.Windows;

internal sealed class WindowsTranscriptionSession(
    ITranscriptionDecoder decoder,
    MMDevice device,
    TranscriptionAudioFormat format) :
    ITranscriptionSession
{
    private const int ChunkMilliseconds = 40;
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private BufferedWaveProvider? sourceBuffer;
    private WasapiCapture? capture;
    private CancellationTokenSource? captureCancellation;
    private Task? capturePump;
    private bool paused;
    private int stopped;
    private int disposed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCapture();
        }
        finally
        {
            _ = stateGate.Release();
        }
    }

    public async IAsyncEnumerable<TranscriptionResult> GetResultsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (TranscriptionResult result in decoder.GetResultsAsync(cancellationToken))
        {
            yield return result;
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await stateGate.WaitAsync(cancellationToken);

        try
        {
            if (!paused && Volatile.Read(ref stopped) == 0)
            {
                paused = true;
                await StopCaptureAsync();
            }
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
            if (paused && Volatile.Read(ref stopped) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                paused = false;
                StartCapture();
            }
        }
        finally
        {
            _ = stateGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0)
        {
            return;
        }

        await stateGate.WaitAsync(cancellationToken);

        try
        {
            await StopCaptureAsync();
            await decoder.CompleteAsync(cancellationToken);
        }
        finally
        {
            _ = stateGate.Release();
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
            await StopAsync();
        }
        catch (Exception)
        {
        }

        lifetime.Cancel();
        await decoder.DisposeAsync();
        device.Dispose();
        lifetime.Dispose();
        stateGate.Dispose();
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
        sampleProvider = sampleProvider.WaveFormat.SampleRate == format.SampleRate
            ? sampleProvider
            : new WdlResamplingSampleProvider(sampleProvider, format.SampleRate);
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
            lifetime.Cancel();
        }
    }

    private async Task CaptureAsync(IWaveProvider waveProvider,
        CancellationToken cancellationToken)
    {
        int bytesPerChunk = format.SampleRate * format.Channels * format.BitsPerSample / 8 * ChunkMilliseconds / 1000;
        byte[] buffer = new byte[bytesPerChunk];
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(ChunkMilliseconds));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            int bytesRead = waveProvider.Read(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                await decoder.AppendAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
        }
    }

    private sealed class DownmixSampleProvider(ISampleProvider source) :
        ISampleProvider
    {
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
