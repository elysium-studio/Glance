using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly ClipboardChangeListener? clipboardChangeListener;
    private readonly DispatcherQueueTimer clipboardPollTimer;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly Dictionary<string, string> localHashesById = [with(StringComparer.Ordinal)];
    private readonly Dictionary<string, string> localIdsByHash = [with(StringComparer.Ordinal)];
    private readonly List<ClipboardEntry> localEntries = [];
    private readonly Dictionary<string, ClipboardSnapshot> localSnapshots = [with(StringComparer.Ordinal)];
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly ITextLocalizer localizer;
    private readonly ClipboardRepository repository;
    private readonly ClipboardShelfViewModel viewModel;
    private readonly GlanceModuleOptions<ClipboardSettings> options;
    private bool isDisposed;
    private uint lastSequenceNumber;

    public ClipboardComponent(ClipboardShelfViewModel viewModel,
        GlanceModuleOptions<ClipboardSettings> options,
        ClipboardRepository repository,
        ModuleResourceTextLocalizer<ClipboardModule> localizer)
    {
        ClipboardDiagnostics.Initialize();
        ClipboardDiagnostics.Write("Component", $"Creating. Diagnostics={ClipboardDiagnostics.FilePath}");

        this.viewModel = viewModel;
        this.options = options;
        this.repository = repository;
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
        RestoreEntries(repository.Load(HistoryLimit));
        PublishEntries();

        try
        {
            clipboardChangeListener = new ClipboardChangeListener();
            clipboardChangeListener.ClipboardChanged += HandleClipboardChanged;
            ClipboardDiagnostics.Write("Listener", $"Registered. Window=0x{clipboardChangeListener.Handle:X}");
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("ListenerRegistrationFailed", exception);
        }

        clipboardPollTimer.Start();
        options.Changed += HandleOptionsChanged;
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

        options.Changed -= HandleOptionsChanged;
    }

    private void HandleClipboardChanged(object? sender, object args)
    {
        uint sequenceNumber = PInvoke.GetClipboardSequenceNumber();
        bool queued = dispatcherQueue.TryEnqueue(() => _ = RefreshAsync());
        ClipboardDiagnostics.Write("ClipboardChanged", $"Sequence={sequenceNumber}; DispatcherQueued={queued}");
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
            ClipboardDiagnostics.Write("CaptureSkipped", $"Could not open clipboard. Sequence={sequenceNumber}");
            return;
        }

        ClipboardSnapshot? snapshot = capture.Snapshot;
        if (snapshot is null)
        {
            lastSequenceNumber = sequenceNumber;
            ClipboardDiagnostics.Write("Capture", $"No supported content. Sequence={sequenceNumber}");
            return;
        }

        string contentHash = CreateContentHash(snapshot);

        if (localIdsByHash.TryGetValue(contentHash, out string? existingId))
        {
            ClipboardEntry? existingEntry = localEntries.Find(entry => entry.Id == existingId);

            if (existingEntry is not null)
            {
                PromoteEntry(existingEntry);
                TryPersist(() => repository.Promote(existingId), "PromoteFailed");
                lastSequenceNumber = sequenceNumber;
                ClipboardDiagnostics.Write("Capture", $"Promoted {DescribeSnapshot(snapshot)}. Sequence={sequenceNumber}; Count={localEntries.Count}");
                return;
            }
        }

        string id = $"Local.{Guid.NewGuid():N}";
        DateTimeOffset timestamp = DateTimeOffset.Now;
        ClipboardEntry entry = CreateEntryFromSnapshot(id, timestamp, snapshot);

        localEntries.Insert(0, entry);
        TrackSnapshot(id, contentHash, snapshot);
        TryPersist(() => repository.Save(CreateRecord(id, contentHash, timestamp, snapshot), HistoryLimit), "SaveFailed");

        while (localEntries.Count > HistoryLimit)
        {
            ClipboardEntry removed = localEntries[^1];
            localEntries.RemoveAt(localEntries.Count - 1);
            UntrackSnapshot(removed.Id);
        }

        lastSequenceNumber = sequenceNumber;
        ClipboardDiagnostics.Write("Capture", $"Added {DescribeSnapshot(snapshot)}. Sequence={sequenceNumber}; Count={localEntries.Count}");
    }

    private int HistoryLimit => (int)Math.Clamp(options.Current.HistoryLimit, 1, 20);

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<ClipboardSettings> args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            while (localEntries.Count > HistoryLimit)
            {
                ClipboardEntry removed = localEntries[^1];
                localEntries.RemoveAt(localEntries.Count - 1);
                UntrackSnapshot(removed.Id);
            }

            TryPersist(() => repository.Trim(HistoryLimit), "TrimFailed");
            PublishEntries();
        });

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
                ClipboardDiagnostics.Write("CopyRejected", $"ListenerAvailable={clipboardChangeListener is not null}; SnapshotAvailable={snapshotAvailable}");
                return false;
            }

            ClipboardDiagnostics.Write("Copy", $"Starting {DescribeSnapshot(snapshot!)}");
            bool copied = await NativeClipboardWriter.WriteAsync(snapshot!, clipboardChangeListener.WindowHandle);

            if (copied)
            {
                lastSequenceNumber = PInvoke.GetClipboardSequenceNumber();
                PromoteEntry(entry);
                TryPersist(() => repository.Promote(entry.Id), "PromoteFailed");
                PublishEntries();
            }

            ClipboardDiagnostics.Write("Copy", $"Completed={copied}; Sequence={PInvoke.GetClipboardSequenceNumber()}");
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
        if (!localSnapshots.ContainsKey(entry.Id))
        {
            return Task.FromResult(false);
        }

        try
        {
            repository.Remove(entry.Id);
            localEntries.RemoveAll(candidate => candidate.Id == entry.Id);
            UntrackSnapshot(entry.Id);
            PublishEntries();
            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException("RemoveFailed", exception);
            return Task.FromResult(false);
        }
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

            repository.Clear();
            localEntries.Clear();
            localSnapshots.Clear();
            localHashesById.Clear();
            localIdsByHash.Clear();
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

    private void RestoreEntries(IReadOnlyList<ClipboardRecord> records)
    {
        foreach (ClipboardRecord record in records)
        {
            ClipboardSnapshot snapshot = new()
            {
                ApplicationLink = record.ApplicationLink,
                Bitmap = record.Bitmap,
                FilePaths = record.FilePaths,
                Html = record.Html,
                Rtf = record.Rtf,
                Text = record.Text,
                WebLink = record.WebLink
            };

            if (!snapshot.HasContent)
            {
                continue;
            }

            localEntries.Add(CreateEntryFromSnapshot(record.Id, record.Timestamp, snapshot));
            TrackSnapshot(record.Id, record.ContentHash, snapshot);
        }
    }

    private void TrackSnapshot(string id,
        string contentHash,
        ClipboardSnapshot snapshot)
    {
        localSnapshots[id] = snapshot;
        localHashesById[id] = contentHash;
        localIdsByHash[contentHash] = id;
    }

    private void UntrackSnapshot(string id)
    {
        localSnapshots.Remove(id);

        if (localHashesById.Remove(id, out string? contentHash))
        {
            localIdsByHash.Remove(contentHash);
        }
    }

    private static ClipboardRecord CreateRecord(string id,
        string contentHash,
        DateTimeOffset timestamp,
        ClipboardSnapshot snapshot) =>
        new(id,
            contentHash,
            timestamp,
            snapshot.Text,
            snapshot.Html,
            snapshot.Rtf,
            snapshot.Bitmap,
            snapshot.FilePaths,
            snapshot.WebLink,
            snapshot.ApplicationLink);

    private static string CreateContentHash(ClipboardSnapshot snapshot)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, snapshot.ApplicationLink);
        AppendBytes(hash, snapshot.Bitmap);
        AppendStrings(hash, snapshot.FilePaths);
        AppendString(hash, snapshot.Html);
        AppendString(hash, snapshot.Rtf);
        AppendString(hash, snapshot.Text);
        AppendString(hash, snapshot.WebLink);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendString(IncrementalHash hash,
        string? value) =>
        AppendBytes(hash, value is null ? null : Encoding.UTF8.GetBytes(value));

    private static void AppendStrings(IncrementalHash hash,
        IReadOnlyList<string>? values)
    {
        AppendInteger(hash, values?.Count ?? -1);

        if (values is null)
        {
            return;
        }

        foreach (string value in values)
        {
            AppendString(hash, value);
        }
    }

    private static void AppendBytes(IncrementalHash hash,
        byte[]? value)
    {
        AppendInteger(hash, value?.Length ?? -1);

        if (value is not null)
        {
            hash.AppendData(value);
        }
    }

    private static void AppendInteger(IncrementalHash hash,
        int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void TryPersist(Action action,
        string stage)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ClipboardDiagnostics.WriteException(stage, exception);
        }
    }

    private ClipboardEntry CreateEntryFromSnapshot(string id,
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
            return CreateEntry(id, localizer.GetText("CopiedImage"), localizer.GetText("KindImage"), "\uEB9F", timestamp);
        }

        string? link = snapshot.WebLink ?? snapshot.ApplicationLink;
        if (!string.IsNullOrWhiteSpace(link))
        {
            return CreateEntry(id, link, localizer.GetText("KindLink"), "\uE71B", timestamp);
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Text))
        {
            string preview = Normalize(snapshot.Text);
            return CreateEntry(id, string.IsNullOrWhiteSpace(preview) ? localizer.GetText("CopiedText") : preview, localizer.GetText("KindText"), "\uE8A5", timestamp);
        }

        if (snapshot.Html is not null)
        {
            return CreateEntry(id, localizer.GetText("RichHtmlContent"), localizer.GetText("KindHtml"), "\uE8D2", timestamp);
        }

        if (snapshot.Rtf is not null)
        {
            return CreateEntry(id, localizer.GetText("FormattedText"), localizer.GetText("KindRichText"), "\uE8D2", timestamp);
        }

        return CreateEntry(id, localizer.GetText("UnsupportedContent"), localizer.GetText("KindOther"), "\uE77F", timestamp);
    }

    private ClipboardEntry CreateEntry(string id,
        string preview,
        string kind,
        string glyph,
        DateTimeOffset timestamp) =>
        new(id, Truncate(preview), kind, glyph, timestamp, localizer);

    private static string GetFileName(string path)
    {
        string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(normalizedPath);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string Normalize(string value) =>
        string.Join(" ",
            value.Split(['\r', '\n', '\t', ' '],
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
