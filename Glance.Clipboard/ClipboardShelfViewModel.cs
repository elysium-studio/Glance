using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Clipboard;

public sealed partial class ClipboardShelfViewModel : ObservableObject
{
    private readonly ITextLocalizer localizer;
    private Func<ClipboardEntry, Task<bool>>? copyEntry;
    private Func<ClipboardEntry, Task<bool>>? pasteEntry;
    private Func<ClipboardEntry, Task<bool>>? removeEntry;
    private Func<Task<bool>>? clearHistory;

    [ObservableProperty]
    private string latestPreview;

    [ObservableProperty]
    private string latestKind;

    [ObservableProperty]
    private string latestGlyph = "\uE77F";

    [ObservableProperty]
    private string historyStatus;

    [ObservableProperty]
    private bool canClearHistory;

    [ObservableProperty]
    private bool canUseSelectedEntry;

    [ObservableProperty]
    private ClipboardEntry? selectedEntry;

    public ClipboardShelfViewModel(ITextLocalizer localizer)
    {
        this.localizer = localizer;
        latestPreview = localizer.GetText("ClipboardEmpty");
        latestKind = localizer.GetText("CopySomethingToBegin");
        historyStatus = localizer.GetText("WaitingForClipboard");
    }

    public string Title => localizer.GetText("ModuleTitle");

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

        LatestPreview = latest?.Preview ?? localizer.GetText("ClipboardEmpty");
        LatestKind = latest?.KindLabel ?? localizer.GetText("CopySomethingToBegin");
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

        HistoryStatus = await copyEntry(entry) ? localizer.GetText("CopiedToClipboard") : localizer.GetText("CopyFailed");
    }

    public async Task PasteAsync(ClipboardEntry entry)
    {
        if (pasteEntry is null)
        {
            return;
        }

        HistoryStatus = await pasteEntry(entry) ? localizer.GetText("SentToFocusedApp") : localizer.GetText("SendFailed");
    }

    public async Task RemoveAsync(ClipboardEntry entry)
    {
        if (removeEntry is null)
        {
            return;
        }

        HistoryStatus = await removeEntry(entry) ? localizer.GetText("RemovedFromHistory") : localizer.GetText("RemoveFailed");
    }

    public async Task ClearAsync()
    {
        if (clearHistory is null)
        {
            return;
        }

        HistoryStatus = await clearHistory() ? localizer.GetText("HistoryCleared") : localizer.GetText("ClearFailed");
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
