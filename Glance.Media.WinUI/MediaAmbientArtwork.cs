using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Glance.Media.WinUI;

internal sealed partial class MediaAmbientArtwork :
    IDisposable
{
    private LoadedImageSurface? surface;

    private MediaAmbientArtwork(LoadedImageSurface surface) =>
        this.surface = surface;

    public LoadedImageSurface Surface =>
        surface ?? throw new ObjectDisposedException(nameof(MediaAmbientArtwork));

    public static Task<MediaAmbientArtwork?> LoadAsync(IRandomAccessStream stream)
    {
        TaskCompletionSource<MediaAmbientArtwork?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LoadedImageSurface surface;

        try
        {
            surface = LoadedImageSurface.StartLoadFromStream(stream, new Size(3, 3));
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        TypedEventHandler<LoadedImageSurface, LoadedImageSourceLoadCompletedEventArgs> handler = null!;
        handler = (sender, args) =>
        {
            sender.LoadCompleted -= handler;
            stream.Dispose();

            if (args.Status == LoadedImageSourceLoadStatus.Success)
            {
                completion.TrySetResult(new MediaAmbientArtwork(surface));
            }
            else
            {
                surface.Dispose();
                completion.TrySetResult(null);
            }
        };
        surface.LoadCompleted += handler;
        return completion.Task;
    }

    public void Dispose()
    {
        surface?.Dispose();
        surface = null;
        GC.SuppressFinalize(this);
    }
}
