using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandContentReader :
    IDesktopIslandContentReader
{
    private static readonly string[] TextDataFormats =
    [
        StandardDataFormats.Text, "System.String", "UnicodeText", "StringFormat", "text/plain"
    ];
    private static readonly string[] WebLinkDataFormats =
    [
        StandardDataFormats.WebLink, StandardDataFormats.ApplicationLink, "UniformResourceLocator", "UniformResourceLocatorW", "text/uri-list", "text/x-moz-url", "text/x-moz-url-data", "Uri", "URL"
    ];

    public IReadOnlyList<GlanceContentKind> GetAvailableKinds(DataPackageView dataView)
    {
        List<GlanceContentKind> kinds = [];

        try
        {
            bool hasText = TryGetDataFormat(dataView, TextDataFormats, out _);

            if (TryGetDataFormat(dataView, WebLinkDataFormats, out _) || hasText)
            {
                kinds.Add(GlanceContentKind.WebLink);
            }

            if (hasText)
            {
                kinds.Add(GlanceContentKind.Text);
            }

            if (dataView.Contains(StandardDataFormats.StorageItems))
            {
                kinds.Add(GlanceContentKind.FilesAndFolders);
            }
        }
        catch (COMException)
        {
        }

        return kinds;
    }

    public async Task<GlanceContentContext?> ReadAsync(DataPackageView dataView, GlanceContentKind kind)
    {
        if (kind == GlanceContentKind.FilesAndFolders)
        {
            IReadOnlyList<IStorageItem> storageItems = await dataView.GetStorageItemsAsync();
            GlanceStorageItem[] items = [.. storageItems.Select(CreateStorageItem).OfType<GlanceStorageItem>()];
            return items.Length == 0 ? null : new GlanceContentContext(kind, items);
        }

        if (kind == GlanceContentKind.WebLink)
        {
            if (dataView.Contains(StandardDataFormats.WebLink))
            {
                Uri? uri = await dataView.GetWebLinkAsync();
                return uri is null ? null : new GlanceContentContext(kind, [], uri.AbsoluteUri);
            }

            if (dataView.Contains(StandardDataFormats.ApplicationLink))
            {
                Uri? uri = await dataView.GetApplicationLinkAsync();
                return uri is null ? null : new GlanceContentContext(kind, [], uri.AbsoluteUri);
            }

            string? link = await GetStringDataAsync(dataView, WebLinkDataFormats) ?? await GetStringDataAsync(dataView, TextDataFormats);
            return Uri.TryCreate(link, UriKind.Absolute, out Uri? parsedUri) ? new GlanceContentContext(kind, [], parsedUri.AbsoluteUri) : null;
        }

        string? text = dataView.Contains(StandardDataFormats.Text) ? await dataView.GetTextAsync() : await GetStringDataAsync(dataView, TextDataFormats);
        return string.IsNullOrWhiteSpace(text) ? null : new GlanceContentContext(kind, [], text);
    }

    private static bool TryGetDataFormat(DataPackageView dataView, IReadOnlyList<string> supportedFormats, out string format)
    {
        foreach (string supportedFormat in supportedFormats)
        {
            if (dataView.Contains(supportedFormat))
            {
                format = supportedFormat;
                return true;
            }
        }

        foreach (string availableFormat in dataView.AvailableFormats)
        {
            if (supportedFormats.Any(supportedFormat => string.Equals(supportedFormat, availableFormat, StringComparison.OrdinalIgnoreCase)))
            {
                format = availableFormat;
                return true;
            }
        }

        format = string.Empty;
        return false;
    }

    private static async Task<string?> GetStringDataAsync(DataPackageView dataView, IReadOnlyList<string> supportedFormats)
    {
        if (!TryGetDataFormat(dataView, supportedFormats, out string format))
        {
            return null;
        }

        object value = await dataView.GetDataAsync(format);
        string? text = value switch
        {
            string stringValue => stringValue, Uri uri => uri.AbsoluteUri, _ => value?.ToString()
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] lines = text.Trim('\0', ' ', '\r', '\n', '\t').Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.FirstOrDefault(line => !line.StartsWith('#'));
    }

    private static GlanceStorageItem? CreateStorageItem(IStorageItem storageItem)
    {
        try
        {
            string path = storageItem.Path;

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(normalizedPath);

            return new GlanceStorageItem(path, string.IsNullOrWhiteSpace(name) ? storageItem.Name : name, storageItem is StorageFolder);
        }
        catch (COMException)
        {
            return null;
        }
    }
}
