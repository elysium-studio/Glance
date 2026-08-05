using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.Stash;

public sealed partial class StashViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private const int ItemLimit = 24;
    private readonly ITextLocalizer localizer = localizer;
    private Func<StashItem, Task>? copyItem;
    private Func<StashItem, Task>? openItem;
    private Func<StashItem, Task>? viewItem;
    private Func<StashItem, Task>? removeItem;

    [ObservableProperty]
    private bool hasItems;

    [ObservableProperty]
    private string compactText = localizer.GetText("EmptySummary");

    [ObservableProperty]
    private StashItem? selectedItem;

    public string Title => localizer.GetText("ModuleTitle");

    public ObservableCollection<StashItem> Items { get; } = [];

    public void ConfigureActions(Func<StashItem, Task> copy,
        Func<StashItem, Task> open,
        Func<StashItem, Task> view,
        Func<StashItem, Task> remove)
    {
        copyItem = copy;
        openItem = open;
        viewItem = view;
        removeItem = remove;
    }

    public void Restore(IEnumerable<StashEntry> entries)
    {
        Items.Clear();

        foreach (StashEntry entry in entries
            .Where(IsValid)
            .OrderByDescending(entry => entry.CreatedAt)
            .Take(ItemLimit))
        {
            Items.Add(CreateItem(entry));
        }

        SelectedItem = Items.FirstOrDefault();
        UpdateState();
    }

    public StashItem? Add(string content,
        bool isLink)
    {
        string normalizedContent = content.Trim();

        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return null;
        }

        StashItemKind kind = isLink || IsWebLink(normalizedContent)
            ? StashItemKind.Link
            : StashItemKind.Text;
        StashItem? existing = Items.FirstOrDefault(item =>
            item.Kind == kind &&
            string.Equals(item.Content, normalizedContent, StringComparison.Ordinal));

        if (existing is not null)
        {
            _ = Items.Remove(existing);
        }

        StashItem item = CreateItem(new StashEntry(existing?.Id ?? Guid.NewGuid().ToString("N"), kind, normalizedContent, DateTimeOffset.Now));
        Items.Insert(0, item);

        while (Items.Count > ItemLimit)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        SelectedItem = item;
        UpdateState();
        return item;
    }

    public Task CopyAsync(StashItem item) => copyItem?.Invoke(item) ?? Task.CompletedTask;

    public Task OpenAsync(StashItem item) => openItem?.Invoke(item) ?? Task.CompletedTask;

    public Task ViewFullTextAsync(StashItem item) => viewItem?.Invoke(item) ?? Task.CompletedTask;

    public async Task RemoveAsync(StashItem item)
    {
        RemoveCore(item);

        if (removeItem is not null)
        {
            await removeItem(item);
        }
    }

    partial void OnSelectedItemChanged(StashItem? value) =>
        CompactText = value?.DisplayText ?? localizer.GetText("EmptySummary");

    private void RemoveCore(StashItem item)
    {
        int index = Items.IndexOf(item);

        if (index < 0)
        {
            return;
        }

        Items.RemoveAt(index);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
        UpdateState();
    }

    private void UpdateState()
    {
        HasItems = Items.Count > 0;
        CompactText = SelectedItem?.DisplayText ?? localizer.GetText("EmptySummary");
    }

    private static bool IsValid(StashEntry entry) => !string.IsNullOrWhiteSpace(entry.Id) &&
        !string.IsNullOrWhiteSpace(entry.Content);

    private static bool IsWebLink(string value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private StashItem CreateItem(StashEntry entry) => new(entry, localizer, Copy, Open, ViewFullText, Remove);

    private async void Copy(StashItem item) => await CopyAsync(item);

    private async void Open(StashItem item) => await OpenAsync(item);

    private async void ViewFullText(StashItem item) => await ViewFullTextAsync(item);

    private async void Remove(StashItem item) => await RemoveAsync(item);
}
