using Microsoft.Windows.AI.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Glance.SpeechToText.WinUI;

public sealed class LiveSpeechAudioCapture :
    IAsyncDisposable
{
    private const int TargetSampleRate = 16000;
    private const int BufferDurationMilliseconds = 40;

    private readonly List<WasapiCapture> captures = [];
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly SpeechAudioProvider provider = new();
    private Task? pumpTask;

    public AudioConfiguration CreateAudioConfiguration() => AudioConfiguration.ForProvider(provider);

    public void Start(SpeechAudioSource audioSource)
    {
        List<ISampleProvider> sources = [];

        if (audioSource is SpeechAudioSource.Microphone or SpeechAudioSource.Meeting)
        {
            sources.Add(CreateSource(new WasapiCapture()));
        }

        if (audioSource is SpeechAudioSource.SystemAudio or SpeechAudioSource.Meeting)
        {
            sources.Add(CreateSource(new WasapiLoopbackCapture()));
        }

        if (sources.Count == 0)
        {
            throw new InvalidOperationException("No audio source was selected.");
        }

        ISampleProvider mixedSource = sources.Count == 1
            ? sources[0]
            : new MixingSampleProvider(sources.Select(source => new VolumeSampleProvider(source)
            {
                Volume = 1f / sources.Count
            }))
            {
                ReadFully = true
            };
        IWaveProvider waveProvider = new SampleToWaveProvider16(mixedSource);

        foreach (WasapiCapture capture in captures)
        {
            capture.StartRecording();
        }

        pumpTask = Task.Run(() => PumpAsync(waveProvider, cancellationTokenSource.Token));
    }

    public async ValueTask DisposeAsync()
    {
        await cancellationTokenSource.CancelAsync();

        foreach (WasapiCapture capture in captures)
        {
            try
            {
                capture.StopRecording();
            }
            catch (Exception)
            {
            }
        }

        if (pumpTask is not null)
        {
            try
            {
                await pumpTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        foreach (WasapiCapture capture in captures)
        {
            capture.Dispose();
        }

        cancellationTokenSource.Dispose();
    }

    private ISampleProvider CreateSource(WasapiCapture capture)
    {
        BufferedWaveProvider buffer = new(capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        capture.DataAvailable += (_, args) => buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);
        captures.Add(capture);

        ISampleProvider sampleProvider = buffer.ToSampleProvider();
        sampleProvider = sampleProvider.WaveFormat.Channels == 1
            ? sampleProvider
            : new DownmixSampleProvider(sampleProvider);

        return sampleProvider.WaveFormat.SampleRate == TargetSampleRate
            ? sampleProvider
            : new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate);
    }

    private async Task PumpAsync(IWaveProvider waveProvider, CancellationToken cancellationToken)
    {
        int bytesPerBuffer = TargetSampleRate * sizeof(short) * BufferDurationMilliseconds / 1000;
        byte[] buffer = new byte[bytesPerBuffer];
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(BufferDurationMilliseconds));

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            int bytesRead = waveProvider.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                continue;
            }

            provider.PushData(bytesRead == buffer.Length ? buffer : buffer[..bytesRead]);
        }
    }

    private sealed class DownmixSampleProvider(ISampleProvider source) :
        ISampleProvider
    {
        private readonly ISampleProvider source = source;
        private readonly float[] sourceBuffer = new float[4096];

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            int sourceChannels = source.WaveFormat.Channels;
            int sourceSamplesRequired = Math.Min(sourceBuffer.Length, count * sourceChannels);
            int sourceSamplesRead = source.Read(sourceBuffer, 0, sourceSamplesRequired);
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
