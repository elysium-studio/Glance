using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Glance.Clipboard;

public partial class ClipboardShelfViewModel : ObservableObject
{
    private Func<ClipboardEntry, Task<bool>>? restoreEntry;

    [ObservableProperty]
    private string latestPreview = "Clipboard is empty";

    [ObservableProperty]
    private string latestKind = "Nothing copied";

    [ObservableProperty]
    private string latestGlyph = "\uE77F";

    [ObservableProperty]
    private string historyStatus = "Waiting for clipboard content";

    public string Title => "Clipboard";

    public ObservableCollection<ClipboardEntry> ShelfItems { get; } = [];

    public void ConfigureRestore(Func<ClipboardEntry, Task<bool>> restore) =>
        restoreEntry = restore;

    public void Update(
        IReadOnlyList<ClipboardEntry> entries,
        string status)
    {
        ClipboardEntry? latest = entries.FirstOrDefault();

        LatestPreview = latest?.Preview ?? "Clipboard is empty";
        LatestKind = latest?.KindLabel ?? "Nothing copied";
        LatestGlyph = latest?.Glyph ?? "\uE77F";
        HistoryStatus = status;

        ShelfItems.Clear();

        foreach (ClipboardEntry entry in entries.Skip(1).Take(3))
        {
            ShelfItems.Add(entry);
        }
    }

    public async Task RestoreAsync(ClipboardEntry entry)
    {
        if (restoreEntry is null)
        {
            return;
        }

        HistoryStatus = await restoreEntry(entry)
            ? "Restored to clipboard"
            : "Could not restore this item";
    }
}
