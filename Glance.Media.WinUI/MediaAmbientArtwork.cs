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

    private readonly object sync = new();
    private LoadedImageSurface? surface;
    private int referenceCount = 1;

    private MediaAmbientArtwork(LoadedImageSurface surface)
    {
        Id = Interlocked.Increment(ref nextId);
        this.surface = surface;
        MediaTransitionDiagnostics.Write("Surface", $"Created Artwork={Id}");
    }

    public int Id { get; }

    public LoadedImageSurface Surface
    {
        get
        {
            lock (sync)
            {
                return surface ?? throw new ObjectDisposedException(nameof(MediaAmbientArtwork));
            }
        }
    }

    public MediaAmbientArtwork Retain()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(surface is null, this);
            referenceCount++;
            MediaTransitionDiagnostics.Write("Surface", $"Retain Artwork={Id} References={referenceCount}");
            return this;
        }
    }

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
        LoadedImageSurface? releasedSurface = null;

        lock (sync)
        {
            if (referenceCount == 0)
            {
                MediaTransitionDiagnostics.Write("Surface", $"Release ignored Artwork={Id} References=0");
                return;
            }

            referenceCount--;
            MediaTransitionDiagnostics.Write("Surface", $"Release Artwork={Id} References={referenceCount}");

            if (referenceCount == 0)
            {
                releasedSurface = surface;
                surface = null;
            }
        }

        if (releasedSurface is not null)
        {
            MediaTransitionDiagnostics.Write("Surface", $"Dispose Artwork={Id}");
            releasedSurface.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
