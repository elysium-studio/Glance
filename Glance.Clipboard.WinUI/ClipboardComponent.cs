using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.Clipboard.WinUI;

public sealed class ClipboardComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly Dictionary<string, ClipboardHistoryItem> historyItems =
        new(StringComparer.Ordinal);
    private readonly ClipboardShelfViewModel viewModel;
    private bool isDisposed;

    public ClipboardComponent(ClipboardShelfViewModel viewModel)
    {
        this.viewModel = viewModel;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        ClipboardCompactView compactView = new(viewModel);
        ClipboardExpandedView expandedView = new(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ConfigureRestore(RestoreAsync);

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged +=
            HandleClipboardChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.HistoryChanged +=
            HandleClipboardChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.HistoryEnabledChanged +=
            HandleClipboardChanged;

        _ = RefreshAsync();
    }

    public string Id => "Clipboard";

    public int Order => 50;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        isDisposed = true;

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -=
            HandleClipboardChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.HistoryChanged -=
            HandleClipboardChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.HistoryEnabledChanged -=
            HandleClipboardChanged;

    }

    private void HandleClipboardChanged(object? sender, object args) =>
        dispatcherQueue.TryEnqueue(() => _ = RefreshAsync());

    private async Task RefreshAsync()
    {
        await refreshGate.WaitAsync();

        try
        {
            if (isDisposed)
            {
                return;
            }

            ClipboardHistoryItemsResult historyResult =
                await Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync();
            List<ClipboardEntry> entries = [];
            string status;

            historyItems.Clear();

            if (historyResult.Status == ClipboardHistoryItemsResultStatus.Success)
            {
                foreach (ClipboardHistoryItem item in historyResult.Items.Take(4))
                {
                    ClipboardEntry entry = await ReadEntryAsync(
                        item.Id,
                        item.Timestamp,
                        item.Content);

                    entries.Add(entry);
                    historyItems[item.Id] = item;
                }

                status = entries.Count > 1
                    ? $"{entries.Count - 1} recent item{(entries.Count == 2 ? string.Empty : "s")}"
                    : "Copy more items to build your shelf";
            }
            else
            {
                DataPackageView current =
                    Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                ClipboardEntry? currentEntry = await TryReadCurrentAsync(current);

                if (currentEntry is not null)
                {
                    entries.Add(currentEntry);
                }

                status = historyResult.Status switch
                {
                    ClipboardHistoryItemsResultStatus.ClipboardHistoryDisabled =>
                        "Turn on Windows clipboard history for the shelf",
                    ClipboardHistoryItemsResultStatus.AccessDenied =>
                        "Clipboard history access is restricted",
                    _ => "Clipboard history is unavailable"
                };
            }

            viewModel.Update(entries, status);
        }
        catch
        {
            if (!isDisposed)
            {
                viewModel.Update([], "Clipboard is temporarily unavailable");
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task<bool> RestoreAsync(ClipboardEntry entry)
    {
        if (!historyItems.TryGetValue(entry.Id, out ClipboardHistoryItem? historyItem))
        {
            return false;
        }

        try
        {
            SetHistoryItemAsContentStatus status =
                Windows.ApplicationModel.DataTransfer.Clipboard.SetHistoryItemAsContent(historyItem);

            return status == SetHistoryItemAsContentStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ClipboardEntry?> TryReadCurrentAsync(DataPackageView content)
    {
        if (content.AvailableFormats.Count == 0)
        {
            return null;
        }

        return await ReadEntryAsync("Current", DateTimeOffset.Now, content);
    }

    private static async Task<ClipboardEntry> ReadEntryAsync(
        string id,
        DateTimeOffset timestamp,
        DataPackageView content)
    {
        try
        {
            if (content.Contains(StandardDataFormats.WebLink))
            {
                Uri link = await content.GetWebLinkAsync();
                return CreateEntry(id, link.ToString(), "Link", "\uE71B", timestamp);
            }

            if (content.Contains(StandardDataFormats.Text))
            {
                string text = await content.GetTextAsync();
                string preview = Normalize(text);

                return CreateEntry(
                    id,
                    string.IsNullOrWhiteSpace(preview) ? "Copied text" : preview,
                    "Text",
                    "\uE8A5",
                    timestamp);
            }

            if (content.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> items = await content.GetStorageItemsAsync();
                string preview = items.Count switch
                {
                    0 => "File or folder",
                    1 => items[0].Name,
                    _ => $"{items[0].Name} and {items.Count - 1} more"
                };

                return CreateEntry(id, preview, "Files", "\uE8B7", timestamp);
            }

            if (content.Contains(StandardDataFormats.Bitmap))
            {
                return CreateEntry(id, "Copied image", "Image", "\uEB9F", timestamp);
            }

            if (content.Contains(StandardDataFormats.Html))
            {
                return CreateEntry(id, "Rich HTML content", "Rich text", "\uE8D2", timestamp);
            }
        }
        catch
        {
            return CreateEntry(id, "Protected clipboard content", "Unavailable", "\uE72E", timestamp);
        }

        return CreateEntry(id, "Unsupported clipboard content", "Other", "\uE77F", timestamp);
    }

    private static ClipboardEntry CreateEntry(
        string id,
        string preview,
        string kind,
        string glyph,
        DateTimeOffset timestamp) =>
        new(id, Truncate(preview), kind, glyph, timestamp);

    private static string Normalize(string value) =>
        string.Join(
            " ",
            value.Split(
                ['\r', '\n', '\t', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Truncate(string value) =>
        value.Length <= 120 ? value : $"{value[..117]}...";
}
