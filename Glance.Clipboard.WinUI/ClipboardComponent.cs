using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Win32;

namespace Glance.Clipboard.WinUI;

public sealed class ClipboardComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private const int MaximumShelfItems = 6;

    private readonly ClipboardChangeListener? clipboardChangeListener;
    private readonly DispatcherQueueTimer clipboardPollTimer;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly Dictionary<string, ClipboardHistoryItem> historyItems =
        new(StringComparer.Ordinal);
    private readonly List<ClipboardEntry> localEntries = [];
    private readonly Dictionary<string, ClipboardSnapshot> localSnapshots =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly List<ClipboardEntry> systemEntries = [];
    private readonly ClipboardShelfViewModel viewModel;
    private bool hasSeededHistory;
    private bool isDisposed;
    private uint lastSequenceNumber;

    public ClipboardComponent(ClipboardShelfViewModel viewModel)
    {
        this.viewModel = viewModel;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        clipboardPollTimer = dispatcherQueue.CreateTimer();
        clipboardPollTimer.Interval = TimeSpan.FromMilliseconds(500);
        clipboardPollTimer.Tick += HandleClipboardPoll;

        ClipboardCompactView compactView = new(viewModel);
        ClipboardExpandedView expandedView = new(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ConfigureActions(CopyAsync, PasteAsync, RemoveAsync, ClearAsync);

        try
        {
            clipboardChangeListener = new ClipboardChangeListener();
            clipboardChangeListener.ClipboardChanged += HandleClipboardChanged;
        }
        catch
        {
            // WinRT notifications remain as a fallback if native registration fails.
        }

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged +=
            HandleClipboardChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.HistoryChanged +=
            HandleClipboardChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.HistoryEnabledChanged +=
            HandleClipboardChanged;

        clipboardPollTimer.Start();
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

        clipboardPollTimer.Stop();
        clipboardPollTimer.Tick -= HandleClipboardPoll;

        if (clipboardChangeListener is not null)
        {
            clipboardChangeListener.ClipboardChanged -= HandleClipboardChanged;
            clipboardChangeListener.Dispose();
        }
    }

    private void HandleClipboardChanged(object? sender, object args) =>
        dispatcherQueue.TryEnqueue(() => _ = RefreshAsync());

    private void HandleClipboardPoll(DispatcherQueueTimer sender, object args)
    {
        uint sequenceNumber = PInvoke.GetClipboardSequenceNumber();
        if (sequenceNumber != 0 && sequenceNumber != lastSequenceNumber)
        {
            _ = RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        await refreshGate.WaitAsync();

        try
        {
            if (isDisposed)
            {
                return;
            }

            if (!hasSeededHistory)
            {
                await SeedSystemHistoryAsync();
            }

            await CaptureCurrentClipboardAsync();
            PublishEntries();
        }
        catch
        {
            PublishEntries();
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task SeedSystemHistoryAsync()
    {
        hasSeededHistory = true;

        try
        {
            ClipboardHistoryItemsResult result =
                await Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync();

            if (result.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                return;
            }

            foreach (ClipboardHistoryItem item in result.Items.Take(MaximumShelfItems))
            {
                ClipboardEntry entry = await ReadEntryAsync(
                    item.Id,
                    item.Timestamp,
                    item.Content);

                systemEntries.Add(entry);
                historyItems[item.Id] = item;
            }
        }
        catch
        {
        }
    }

    private async Task CaptureCurrentClipboardAsync()
    {
        uint sequenceNumber = PInvoke.GetClipboardSequenceNumber();
        if (sequenceNumber != 0 && sequenceNumber == lastSequenceNumber)
        {
            return;
        }

        NativeClipboardCapture capture = await NativeClipboardReader.CaptureAsync();
        if (!capture.WasRead)
        {
            return;
        }

        ClipboardSnapshot? snapshot = capture.Snapshot;
        if (snapshot is null)
        {
            lastSequenceNumber = sequenceNumber;
            return;
        }

        string id = $"Local.{Guid.NewGuid():N}";
        ClipboardEntry entry = CreateEntryFromSnapshot(id, DateTimeOffset.Now, snapshot);

        localEntries.Insert(0, entry);
        localSnapshots[id] = snapshot;

        while (localEntries.Count > MaximumShelfItems)
        {
            ClipboardEntry removed = localEntries[^1];
            localEntries.RemoveAt(localEntries.Count - 1);
            localSnapshots.Remove(removed.Id);
        }

        lastSequenceNumber = sequenceNumber;
    }

    private void PublishEntries()
    {
        List<ClipboardEntry> entries = [.. localEntries];

        foreach (ClipboardEntry entry in systemEntries)
        {
            if (entries.Count >= MaximumShelfItems)
            {
                break;
            }

            bool isDuplicate = entries.Any(existing =>
                existing.Preview == entry.Preview &&
                existing.KindLabel == entry.KindLabel);

            if (!isDuplicate)
            {
                entries.Add(entry);
            }
        }

        string status = entries.Count switch
        {
            0 => "No recent clips",
            1 => "1 recent clip",
            _ => $"{entries.Count} recent clips"
        };

        viewModel.Update(entries, status);
    }

    private async Task<bool> CopyAsync(ClipboardEntry entry)
    {
        bool copied;

        if (localSnapshots.TryGetValue(entry.Id, out ClipboardSnapshot? snapshot))
        {
            copied = await snapshot.CopyAsync();
        }
        else if (historyItems.TryGetValue(entry.Id, out ClipboardHistoryItem? historyItem))
        {
            try
            {
                SetHistoryItemAsContentStatus status =
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetHistoryItemAsContent(historyItem);
                copied = status == SetHistoryItemAsContentStatus.Success;
            }
            catch
            {
                copied = false;
            }
        }
        else
        {
            copied = false;
        }

        if (copied)
        {
            lastSequenceNumber = PInvoke.GetClipboardSequenceNumber();
            PromoteEntry(entry);
            PublishEntries();
        }

        return copied;
    }

    private async Task<bool> PasteAsync(ClipboardEntry entry)
    {
        if (!await CopyAsync(entry))
        {
            return false;
        }

        await Task.Delay(40);
        return FocusedWindowPaste.Send();
    }

    private Task<bool> RemoveAsync(ClipboardEntry entry)
    {
        try
        {
            bool removed = false;

            if (localSnapshots.Remove(entry.Id))
            {
                localEntries.RemoveAll(candidate => candidate.Id == entry.Id);
                removed = true;

                foreach (ClipboardEntry duplicate in systemEntries
                    .Where(candidate =>
                        candidate.Preview == entry.Preview &&
                        candidate.KindLabel == entry.KindLabel)
                    .ToArray())
                {
                    RemoveSystemEntry(duplicate);
                }
            }
            else if (historyItems.ContainsKey(entry.Id))
            {
                removed = RemoveSystemEntry(entry);
            }

            PublishEntries();
            return Task.FromResult(removed);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private bool RemoveSystemEntry(ClipboardEntry entry)
    {
        if (!historyItems.Remove(entry.Id, out ClipboardHistoryItem? historyItem))
        {
            return false;
        }

        systemEntries.RemoveAll(candidate => candidate.Id == entry.Id);

        try
        {
            Windows.ApplicationModel.DataTransfer.Clipboard.DeleteItemFromHistory(historyItem);
        }
        catch
        {
        }

        return true;
    }

    private Task<bool> ClearAsync()
    {
        try
        {
            localEntries.Clear();
            localSnapshots.Clear();
            systemEntries.Clear();
            historyItems.Clear();

            try
            {
                Windows.ApplicationModel.DataTransfer.Clipboard.ClearHistory();
            }
            catch
            {
            }

            Windows.ApplicationModel.DataTransfer.Clipboard.Clear();
            lastSequenceNumber = PInvoke.GetClipboardSequenceNumber();
            PublishEntries();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private void PromoteEntry(ClipboardEntry entry)
    {
        if (localEntries.Remove(entry))
        {
            localEntries.Insert(0, entry);
            return;
        }

        if (systemEntries.Remove(entry))
        {
            systemEntries.Insert(0, entry);
        }
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

            if (content.Contains(StandardDataFormats.ApplicationLink))
            {
                Uri link = await content.GetApplicationLinkAsync();
                return CreateEntry(id, link.ToString(), "App link", "\uE71B", timestamp);
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
                return CreateEntry(id, "Rich HTML content", "HTML", "\uE8D2", timestamp);
            }

            if (content.Contains(StandardDataFormats.Rtf))
            {
                return CreateEntry(id, "Formatted text", "Rich text", "\uE8D2", timestamp);
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
        }
        catch
        {
            return CreateEntry(id, "Protected clipboard content", "Unavailable", "\uE72E", timestamp);
        }

        return CreateEntry(id, "Unsupported clipboard content", "Other", "\uE77F", timestamp);
    }

    private static ClipboardEntry CreateEntryFromSnapshot(
        string id,
        DateTimeOffset timestamp,
        ClipboardSnapshot snapshot)
    {
        if (snapshot.StorageItems is { Count: > 0 } items)
        {
            string preview = items.Count == 1
                ? items[0].Name
                : $"{items[0].Name} and {items.Count - 1} more";

            return CreateEntry(id, preview, "Files", "\uE8B7", timestamp);
        }

        if (snapshot.Bitmap is not null)
        {
            return CreateEntry(id, "Copied image", "Image", "\uEB9F", timestamp);
        }

        string? link = snapshot.WebLink ?? snapshot.ApplicationLink;
        if (!string.IsNullOrWhiteSpace(link))
        {
            return CreateEntry(id, link, "Link", "\uE71B", timestamp);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Text))
        {
            string preview = Normalize(snapshot.Text);
            return CreateEntry(
                id,
                string.IsNullOrWhiteSpace(preview) ? "Copied text" : preview,
                "Text",
                "\uE8A5",
                timestamp);
        }

        if (snapshot.Html is not null)
        {
            return CreateEntry(id, "Rich HTML content", "HTML", "\uE8D2", timestamp);
        }

        if (snapshot.Rtf is not null)
        {
            return CreateEntry(id, "Formatted text", "Rich text", "\uE8D2", timestamp);
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
