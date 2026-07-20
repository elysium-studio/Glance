using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly List<ClipboardEntry> localEntries = [];
    private readonly Dictionary<string, ClipboardSnapshot> localSnapshots =
        new(StringComparer.Ordinal);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly ClipboardShelfViewModel viewModel;
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
            // Sequence polling remains available if native notification registration fails.
        }

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
        string status = localEntries.Count switch
        {
            0 => "No recent clips",
            1 => "1 recent clip",
            _ => $"{localEntries.Count} recent clips"
        };

        viewModel.Update(localEntries, status);
    }

    private async Task<bool> CopyAsync(ClipboardEntry entry)
    {
        if (clipboardChangeListener is null ||
            !localSnapshots.TryGetValue(entry.Id, out ClipboardSnapshot? snapshot))
        {
            return false;
        }

        bool copied = await NativeClipboardWriter.WriteAsync(
            snapshot,
            clipboardChangeListener.WindowHandle);

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
        bool removed = localSnapshots.Remove(entry.Id);
        if (removed)
        {
            localEntries.RemoveAll(candidate => candidate.Id == entry.Id);
            PublishEntries();
        }

        return Task.FromResult(removed);
    }

    private async Task<bool> ClearAsync()
    {
        if (clipboardChangeListener is null ||
            !await NativeClipboardWriter.ClearAsync(clipboardChangeListener.WindowHandle))
        {
            return false;
        }

        localEntries.Clear();
        localSnapshots.Clear();
        lastSequenceNumber = PInvoke.GetClipboardSequenceNumber();
        PublishEntries();
        return true;
    }

    private void PromoteEntry(ClipboardEntry entry)
    {
        if (localEntries.Remove(entry))
        {
            localEntries.Insert(0, entry);
        }
    }

    private static ClipboardEntry CreateEntryFromSnapshot(
        string id,
        DateTimeOffset timestamp,
        ClipboardSnapshot snapshot)
    {
        if (snapshot.FilePaths is { Count: > 0 } paths)
        {
            string firstName = GetFileName(paths[0]);
            string preview = paths.Count == 1
                ? firstName
                : $"{firstName} and {paths.Count - 1} more";

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

    private static string GetFileName(string path)
    {
        string normalizedPath = path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(normalizedPath);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string Normalize(string value) =>
        string.Join(
            " ",
            value.Split(
                ['\r', '\n', '\t', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Truncate(string value) =>
        value.Length <= 120 ? value : $"{value[..117]}...";
}
