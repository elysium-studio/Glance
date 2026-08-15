using Glance.Transcription;
using NAudio.CoreAudioApi;

namespace Glance.Transcription.Windows;

public sealed class WindowsAudioInputSourceCatalog :
    IAudioInputSourceCatalog
{
    public Task<IReadOnlyList<AudioInputSource>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<AudioInputSource>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using MMDeviceEnumerator enumerator = new();
            string? defaultDeviceId = null;

            try
            {
                defaultDeviceId = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia).ID;
            }
            catch (Exception)
            {
            }

            return (AudioInputSource[])[.. enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(device => new AudioInputSource(device.ID,
                    device.FriendlyName,
                    string.Equals(device.ID, defaultDeviceId, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(source => source.IsDefault)
                .ThenBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
        }, cancellationToken);
}
