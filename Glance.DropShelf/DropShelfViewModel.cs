using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.DropShelf;

public partial class DropShelfViewModel : ObservableObject
{
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private string summary;

    [ObservableProperty]
    private string detail;

    [ObservableProperty]
    private bool hasItems;

    public DropShelfViewModel(ITextLocalizer localizer)
    {
        this.localizer = localizer;
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
