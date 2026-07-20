using Glance.Application.Abstractions;
using Glance.UI.WinUI;
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
    private readonly ITextLocalizer localizer;
    private readonly ClipboardShelfViewModel viewModel;
    private bool isDisposed;
    private uint lastSequenceNumber;

    public ClipboardComponent(
        ClipboardShelfViewModel viewModel,
        ModuleResourceTextLocalizer<ClipboardModule> localizer)
    {
        ClipboardDiagnostics.Initialize();
        ClipboardDiagnostics.Write("Component", $"Creating. Diagnostics={ClipboardDiagnostics.FilePath}");

        this.viewModel = viewModel;
        this.localizer = localizer;
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
            ClipboardDiagnostics.Write(
                "Listener",
                $"Registered. Window=0x{clipboardChangeListener.Handle:X}");
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("ListenerRegistrationFailed", exception);
            // Sequence polling remains available if native notification registration fails.
        }

        clipboardPollTimer.Start();
        _ = RefreshAsync();
    }

    public string Id => "Clipboard";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

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

    private void HandleClipboardChanged(object? sender, object args)
    {
        uint sequenceNumber = PInvoke.GetClipboardSequenceNumber();
        bool queued = dispatcherQueue.TryEnqueue(() => _ = RefreshAsync());
        ClipboardDiagnostics.Write(
            "ClipboardChanged",
            $"Sequence={sequenceNumber}; DispatcherQueued={queued}");
    }

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
        using IDisposable operation = ClipboardDiagnostics.Begin("Refresh");
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
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("RefreshFailed", exception);
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
            ClipboardDiagnostics.Write(
                "CaptureSkipped",
                $"Could not open clipboard. Sequence={sequenceNumber}");
            return;
        }

        ClipboardSnapshot? snapshot = capture.Snapshot;
        if (snapshot is null)
        {
            lastSequenceNumber = sequenceNumber;
            ClipboardDiagnostics.Write("Capture", $"No supported content. Sequence={sequenceNumber}");
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
        ClipboardDiagnostics.Write(
            "Capture",
            $"Added {DescribeSnapshot(snapshot)}. Sequence={sequenceNumber}; Count={localEntries.Count}");
    }

    private void PublishEntries()
    {
        string status = localEntries.Count switch
        {
            0 => localizer.GetText("RecentClipsNone"),
            1 => localizer.GetText("RecentClipsOne"),
            _ => localizer.GetText("RecentClipsMany", localEntries.Count)
        };

        viewModel.Update(localEntries, status);
    }

    private async Task<bool> CopyAsync(ClipboardEntry entry)
    {
        using IDisposable operation = ClipboardDiagnostics.Begin("Copy");

        try
        {
            bool snapshotAvailable =
                localSnapshots.TryGetValue(entry.Id, out ClipboardSnapshot? snapshot);

            if (clipboardChangeListener is null || !snapshotAvailable)
            {
                ClipboardDiagnostics.Write(
                    "CopyRejected",
                    $"ListenerAvailable={clipboardChangeListener is not null}; SnapshotAvailable={snapshotAvailable}");
                return false;
            }

            ClipboardDiagnostics.Write("Copy", $"Starting {DescribeSnapshot(snapshot!)}");
            bool copied = await NativeClipboardWriter.WriteAsync(
                snapshot!,
                clipboardChangeListener.WindowHandle);

            if (copied)
            {
                lastSequenceNumber = PInvoke.GetClipboardSequenceNumber();
                PromoteEntry(entry);
                PublishEntries();
            }

            ClipboardDiagnostics.Write(
                "Copy",
                $"Completed={copied}; Sequence={PInvoke.GetClipboardSequenceNumber()}");
            return copied;
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("CopyFailed", exception);
            return false;
        }
    }

    private async Task<bool> PasteAsync(ClipboardEntry entry)
    {
        using IDisposable operation = ClipboardDiagnostics.Begin("Paste");

        try
        {
            if (!await CopyAsync(entry))
            {
                return false;
            }

            await Task.Delay(40);
            bool sent = FocusedWindowPaste.Send();
            ClipboardDiagnostics.Write("Paste", $"InputSent={sent}");
            return sent;
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("PasteFailed", exception);
            return false;
        }
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
        using IDisposable operation = ClipboardDiagnostics.Begin("Clear");

        try
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
            ClipboardDiagnostics.Write("Clear", $"Completed. Sequence={lastSequenceNumber}");
            return true;
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("ClearFailed", exception);
            return false;
        }
    }

    private void PromoteEntry(ClipboardEntry entry)
    {
        if (localEntries.Remove(entry))
        {
            localEntries.Insert(0, entry);
        }
    }

    private ClipboardEntry CreateEntryFromSnapshot(
        string id,
        DateTimeOffset timestamp,
        ClipboardSnapshot snapshot)
    {
        if (snapshot.FilePaths is { Count: > 0 } paths)
        {
            string firstName = GetFileName(paths[0]);
            string preview = paths.Count == 1
                ? firstName
                : localizer.GetText("FilesAndMore", firstName, paths.Count - 1);

            return CreateEntry(id, preview, localizer.GetText("KindFiles"), "\uE8B7", timestamp);
        }

        if (snapshot.Bitmap is not null)
        {
            return CreateEntry(
                id,
                localizer.GetText("CopiedImage"),
                localizer.GetText("KindImage"),
                "\uEB9F",
                timestamp);
        }

        string? link = snapshot.WebLink ?? snapshot.ApplicationLink;
        if (!string.IsNullOrWhiteSpace(link))
        {
            return CreateEntry(id, link, localizer.GetText("KindLink"), "\uE71B", timestamp);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Text))
        {
            string preview = Normalize(snapshot.Text);
            return CreateEntry(
                id,
                string.IsNullOrWhiteSpace(preview) ? localizer.GetText("CopiedText") : preview,
                localizer.GetText("KindText"),
                "\uE8A5",
                timestamp);
        }

        if (snapshot.Html is not null)
        {
            return CreateEntry(
                id,
                localizer.GetText("RichHtmlContent"),
                localizer.GetText("KindHtml"),
                "\uE8D2",
                timestamp);
        }

        if (snapshot.Rtf is not null)
        {
            return CreateEntry(
                id,
                localizer.GetText("FormattedText"),
                localizer.GetText("KindRichText"),
                "\uE8D2",
                timestamp);
        }

        return CreateEntry(
            id,
            localizer.GetText("UnsupportedContent"),
            localizer.GetText("KindOther"),
            "\uE77F",
            timestamp);
    }

    private ClipboardEntry CreateEntry(
        string id,
        string preview,
        string kind,
        string glyph,
        DateTimeOffset timestamp) =>
        new(id, Truncate(preview), kind, glyph, timestamp, localizer);

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

    private static string DescribeSnapshot(ClipboardSnapshot snapshot)
    {
        List<string> formats = [];

        if (snapshot.Text is not null)
        {
            formats.Add($"Text({snapshot.Text.Length})");
        }

        if (snapshot.Html is not null)
        {
            formats.Add($"Html({snapshot.Html.Length})");
        }

        if (snapshot.Rtf is not null)
        {
            formats.Add($"Rtf({snapshot.Rtf.Length})");
        }

        if (snapshot.Bitmap is not null)
        {
            formats.Add($"Png({snapshot.Bitmap.Length})");
        }

        if (snapshot.FilePaths is { Count: > 0 } paths)
        {
            formats.Add($"Files({paths.Count})");
        }

        if (snapshot.WebLink is not null || snapshot.ApplicationLink is not null)
        {
            formats.Add("Link");
        }

        return formats.Count == 0 ? "Formats=None" : $"Formats={string.Join(',', formats)}";
    }
}
