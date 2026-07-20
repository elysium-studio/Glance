using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Glance.Clipboard;

public partial class ClipboardShelfViewModel : ObservableObject
{
    private Func<ClipboardEntry, Task<bool>>? copyEntry;
    private Func<ClipboardEntry, Task<bool>>? pasteEntry;
    private Func<ClipboardEntry, Task<bool>>? removeEntry;
    private Func<Task<bool>>? clearHistory;

    [ObservableProperty]
    private string latestPreview = "Clipboard is empty";

    [ObservableProperty]
    private string latestKind = "Nothing copied";

    [ObservableProperty]
    private string latestGlyph = "\uE77F";

    [ObservableProperty]
    private string historyStatus = "Waiting for clipboard content";

    [ObservableProperty]
    private bool canClearHistory;

    [ObservableProperty]
    private bool canUseSelectedEntry;

    [ObservableProperty]
    private ClipboardEntry? selectedEntry;

    public string Title => "Clipboard";

    public ObservableCollection<ClipboardEntry> ShelfItems { get; } = [];

    public void ConfigureActions(
        Func<ClipboardEntry, Task<bool>> copy,
        Func<ClipboardEntry, Task<bool>> paste,
        Func<ClipboardEntry, Task<bool>> remove,
        Func<Task<bool>> clear)
    {
        copyEntry = copy;
        pasteEntry = paste;
        removeEntry = remove;
        clearHistory = clear;
    }

    public void Update(
        IReadOnlyList<ClipboardEntry> entries,
        string status)
    {
        ClipboardEntry? latest = entries.FirstOrDefault();

        LatestPreview = latest?.Preview ?? "Clipboard is empty";
        LatestKind = latest?.KindLabel ?? "Nothing copied";
        LatestGlyph = latest?.Glyph ?? "\uE77F";
        HistoryStatus = status;
        CanClearHistory = entries.Count > 0;

        ShelfItems.Clear();

        foreach (ClipboardEntry entry in entries.Take(6))
        {
            ShelfItems.Add(entry);
        }

        SelectedEntry = latest;
    }

    partial void OnSelectedEntryChanged(ClipboardEntry? value) =>
        CanUseSelectedEntry = value is not null;

    public async Task CopyAsync(ClipboardEntry entry)
    {
        if (copyEntry is null)
        {
            return;
        }

        HistoryStatus = await copyEntry(entry)
            ? "Copied to clipboard"
            : "Could not copy this item";
    }

    public async Task PasteAsync(ClipboardEntry entry)
    {
        if (pasteEntry is null)
        {
            return;
        }

        HistoryStatus = await pasteEntry(entry)
            ? "Sent to the focused app"
            : "Could not send this item";
    }

    public async Task RemoveAsync(ClipboardEntry entry)
    {
        if (removeEntry is null)
        {
            return;
        }

        HistoryStatus = await removeEntry(entry)
            ? "Removed from clipboard history"
            : "Could not remove this item";
    }

    public async Task ClearAsync()
    {
        if (clearHistory is null)
        {
            return;
        }

        HistoryStatus = await clearHistory()
            ? "Clipboard history cleared"
            : "Could not clear clipboard history";
    }

    public async Task CopySelectedAsync()
    {
        if (SelectedEntry is not null)
        {
            await CopyAsync(SelectedEntry);
        }
    }

    public async Task PasteSelectedAsync()
    {
        if (SelectedEntry is not null)
        {
            await PasteAsync(SelectedEntry);
        }
    }

    public async Task RemoveSelectedAsync()
    {
        if (SelectedEntry is not null)
        {
            await RemoveAsync(SelectedEntry);
        }
    }
}
