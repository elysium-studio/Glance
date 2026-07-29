using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Glance.Media.WinUI;

internal sealed partial class MediaAmbientArtwork :
    IDisposable
{
    private static int nextId;

    private LoadedImageSurface? surface;

    private MediaAmbientArtwork(LoadedImageSurface surface)
    {
        Id = Interlocked.Increment(ref nextId);
        this.surface = surface;
        MediaTransitionDiagnostics.Write("Surface", $"Created Artwork={Id}");
    }

    public int Id { get; }

    public LoadedImageSurface Surface =>
        surface ?? throw new ObjectDisposedException(nameof(MediaAmbientArtwork));

    public static Task<MediaAmbientArtwork?> LoadAsync(IRandomAccessStream stream)
    {
        TaskCompletionSource<MediaAmbientArtwork?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LoadedImageSurface surface;
        MediaTransitionDiagnostics.Write("Surface", $"Load requested StreamSize={stream.Size}");

        try
        {
            surface = LoadedImageSurface.StartLoadFromStream(stream, new Size(3, 3));
        }
        catch (Exception exception)
        {
            MediaTransitionDiagnostics.Write("Surface", $"Load start failed Error={exception.GetType().Name} HResult=0x{exception.HResult:X8}");
            stream.Dispose();
            throw;
        }

        TypedEventHandler<LoadedImageSurface, LoadedImageSourceLoadCompletedEventArgs> handler = null!;
        handler = (sender, args) =>
        {
            sender.LoadCompleted -= handler;
            stream.Dispose();
            MediaTransitionDiagnostics.Write("Surface", $"Load completed Status={args.Status}");

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
        MediaTransitionDiagnostics.Write("Surface", $"Dispose Artwork={Id} HasSurface={surface is not null}");
        surface?.Dispose();
        surface = null;
        GC.SuppressFinalize(this);
    }
}
