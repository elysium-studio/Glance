using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Glance.Clipboard.WinUI;

internal sealed class ClipboardSnapshot
{
    private const ulong MaximumBitmapBytes = 32 * 1024 * 1024;

    internal ClipboardSnapshot()
    {
    }

    public string? ApplicationLink { get; internal set; }

    public byte[]? Bitmap { get; internal set; }

    public string? Html { get; internal set; }

    public string? Rtf { get; internal set; }

    public IReadOnlyList<IStorageItem>? StorageItems { get; internal set; }

    public string? Text { get; internal set; }

    public string? WebLink { get; internal set; }

    public bool HasContent =>
        ApplicationLink is not null ||
        Bitmap is not null ||
        Html is not null ||
        Rtf is not null ||
        StorageItems is not null ||
        Text is not null ||
        WebLink is not null;

    public static async Task<ClipboardSnapshot?> CaptureAsync(DataPackageView content)
    {
        ClipboardSnapshot snapshot = new();

        try
        {
            if (content.Contains(StandardDataFormats.Text))
            {
                snapshot.Text = await content.GetTextAsync();
            }
        }
        catch
        {
        }

        try
        {
            if (content.Contains(StandardDataFormats.Html))
            {
                snapshot.Html = await content.GetHtmlFormatAsync();
            }
        }
        catch
        {
        }

        try
        {
            if (content.Contains(StandardDataFormats.Rtf))
            {
                snapshot.Rtf = await content.GetRtfAsync();
            }
        }
        catch
        {
        }

        try
        {
            if (content.Contains(StandardDataFormats.WebLink))
            {
                snapshot.WebLink = (await content.GetWebLinkAsync()).ToString();
            }
        }
        catch
        {
        }

        try
        {
            if (content.Contains(StandardDataFormats.ApplicationLink))
            {
                snapshot.ApplicationLink = (await content.GetApplicationLinkAsync()).ToString();
            }
        }
        catch
        {
        }

        try
        {
            if (content.Contains(StandardDataFormats.StorageItems))
            {
                snapshot.StorageItems = await content.GetStorageItemsAsync();
            }
        }
        catch
        {
        }

        try
        {
            if (content.Contains(StandardDataFormats.Bitmap))
            {
                RandomAccessStreamReference bitmapReference = await content.GetBitmapAsync();
                using IRandomAccessStreamWithContentType stream = await bitmapReference.OpenReadAsync();

                if (stream.Size <= MaximumBitmapBytes)
                {
                    using DataReader reader = new(stream.GetInputStreamAt(0));
                    uint size = (uint)stream.Size;
                    await reader.LoadAsync(size);
                    snapshot.Bitmap = new byte[size];
                    reader.ReadBytes(snapshot.Bitmap);
                }
            }
        }
        catch
        {
        }

        return snapshot.HasContent ? snapshot : null;
    }

    public async Task<bool> CopyAsync()
    {
        InMemoryRandomAccessStream? bitmapStream = null;

        try
        {
            DataPackage package = new();

            if (Text is not null)
            {
                package.SetText(Text);
            }

            if (Html is not null)
            {
                package.SetHtmlFormat(Html);
            }

            if (Rtf is not null)
            {
                package.SetRtf(Rtf);
            }

            if (WebLink is not null && Uri.TryCreate(WebLink, UriKind.Absolute, out Uri? webLink))
            {
                package.SetWebLink(webLink);
            }

            if (ApplicationLink is not null &&
                Uri.TryCreate(ApplicationLink, UriKind.Absolute, out Uri? applicationLink))
            {
                package.SetApplicationLink(applicationLink);
            }

            if (StorageItems is not null)
            {
                package.SetStorageItems(StorageItems);
            }

            if (Bitmap is not null)
            {
                bitmapStream = new InMemoryRandomAccessStream();
                using DataWriter writer = new(bitmapStream);
                writer.WriteBytes(Bitmap);
                await writer.StoreAsync();
                writer.DetachStream();
                bitmapStream.Seek(0);
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(bitmapStream));
            }

            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            bitmapStream?.Dispose();
        }
    }
}
