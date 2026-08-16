using Glance.Transcription;
using NAudio.CoreAudioApi;

namespace Glance.Transcription.Windows;

public sealed class WindowsTranscriptionSessionFactory(ITranscriptionDecoderFactory decoders) :
    ITranscriptionSessionFactory
{
    public async Task<ITranscriptionSession> CreateAsync(TranscriptionSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        TranscriptionAudioFormat format = decoders.GetAudioFormat(options.ModelId);

        if (format.Channels != 1 || format.BitsPerSample != 16 || format.SampleRate <= 0)
        {
            throw new NotSupportedException("The transcription provider requested an unsupported audio format");
        }

        ITranscriptionDecoder decoder = await decoders.CreateDecoderAsync(options.ModelId,
            options.Language,
            cancellationToken);
        MMDevice? device = null;

        try
        {
            using MMDeviceEnumerator enumerator = new();
            device = enumerator.GetDevice(options.AudioInputSourceId);
            WindowsTranscriptionSession session = new(decoder, device, format);
            device = null;
            await session.StartAsync(cancellationToken);
            return session;
        }
        catch
        {
            device?.Dispose();
            await decoder.DisposeAsync();
            throw;
        }
    }
}
