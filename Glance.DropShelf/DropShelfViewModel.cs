using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.DropShelf;

public sealed partial class DropShelfViewModel : ObservableObject
{
    private readonly ITextLocalizer localizer;
    private int itemLimit;

    [ObservableProperty]
    private string summary;

    [ObservableProperty]
    private string detail;

    [ObservableProperty]
    private bool hasItems;

    public DropShelfViewModel(ITextLocalizer localizer, DropShelfSettings? settings = null)
    {
        this.localizer = localizer;
        itemLimit = GetItemLimit(settings ?? new DropShelfSettings());
        summary = localizer.GetText("EmptySummary");
        detail = localizer.GetText("EmptyDetail");
    }

    public string Title => localizer.GetText("ModuleTitle");

    public string DragCaption => localizer.GetText("DragCaption");

    public ObservableCollection<DropShelfItem> Items { get; } = [];

    public void AddItems(IEnumerable<DropShelfItem> items)
    {
        HashSet<string> existingPaths = Items
            .Select(item => item.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (DropShelfItem item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Path) && existingPaths.Add(item.Path))
            {
                Items.Add(item);
            }
        }

        TrimItems();

        UpdateState();
    }

    public void Remove(DropShelfItem item)
    {
        Items.Remove(item);
        UpdateState();
    }

    public void Clear()
    {
        Items.Clear();
        UpdateState();
    }

    public void RemoveMissingItems()
    {
        for (int index = Items.Count - 1; index >= 0; index--)
        {
            if (!File.Exists(Items[index].Path) && !Directory.Exists(Items[index].Path))
            {
                Items.RemoveAt(index);
            }
        }

        UpdateState();
    }

    public void ApplySettings(DropShelfSettings settings)
    {
        itemLimit = GetItemLimit(settings);
        TrimItems();
        UpdateState();
    }

    private void TrimItems()
    {
        while (Items.Count > itemLimit)
        {
            Items.RemoveAt(0);
        }
    }

    private static int GetItemLimit(DropShelfSettings settings) =>
        (int)Math.Clamp(settings.ItemLimit, 1, 50);

    private void UpdateState()
    {
        HasItems = Items.Count > 0;
        Summary = Items.Count switch
        {
            0 => localizer.GetText("EmptySummary"),
            1 => localizer.GetText("OneItemSummary"),
            _ => localizer.GetText("ManyItemsSummary", Items.Count)
        };
        Detail = HasItems
            ? localizer.GetText("ReadyDetail") : localizer.GetText("EmptyDetail");
    }
}
