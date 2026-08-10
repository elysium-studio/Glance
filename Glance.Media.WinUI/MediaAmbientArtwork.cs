using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Glance.Media.WinUI;

internal sealed partial class MediaAmbientArtwork :
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly object sync = new();
    private LoadedImageSurface? surface;
    private int referenceCount = 1;

    private MediaAmbientArtwork(LoadedImageSurface surface, DispatcherQueue dispatcherQueue)
    {
        this.surface = surface;
        this.dispatcherQueue = dispatcherQueue;
    }

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
            return this;
        }
    }

    public static Task<MediaAmbientArtwork?> LoadAsync(IRandomAccessStream stream)
    {
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread() ??
            throw new InvalidOperationException("Ambient artwork must be created on a dispatcher thread.");
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
        handler = (_, args) =>
        {
            LoadedImageSourceLoadStatus status = args.Status;

            void CompleteLoad()
            {
                surface.LoadCompleted -= handler;
                stream.Dispose();

                if (status == LoadedImageSourceLoadStatus.Success)
                {
                    completion.TrySetResult(new MediaAmbientArtwork(surface, dispatcherQueue));
                }
                else
                {
                    CloseSurface(surface);
                    completion.TrySetResult(null);
                }
            }

            if (dispatcherQueue.HasThreadAccess)
            {
                CompleteLoad();
            }
            else if (!dispatcherQueue.TryEnqueue(CompleteLoad))
            {
                completion.TrySetException(new InvalidOperationException("The media dispatcher rejected artwork load completion."));
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
                return;
            }

            referenceCount--;

            if (referenceCount == 0)
            {
                releasedSurface = surface;
                surface = null;
            }
        }

        if (releasedSurface is not null)
        {
            DisposeSurface(releasedSurface);
        }

        GC.SuppressFinalize(this);
    }

    private void DisposeSurface(LoadedImageSurface releasedSurface)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            CloseSurface(releasedSurface);
        }
        else
        {
            _ = dispatcherQueue.TryEnqueue(() => CloseSurface(releasedSurface));
        }
    }

    private static void CloseSurface(LoadedImageSurface releasedSurface)
    {
        try
        {
            releasedSurface.Dispose();
        }
        catch (InvalidCastException)
        {
            // During XAML teardown the WinRT object can be disconnected before its
            // final managed owner releases it, so IClosable is no longer queryable.
        }
    }
}
