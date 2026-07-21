using Glance.DropShelf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Glance.DropShelf.WinUI;

public sealed class DropShelfTransferStore
{
    private readonly ConcurrentDictionary<string, IStorageItem> storageItems =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DropShelfItem>> StageAsync(
        IEnumerable<DropShelfItem> items)
    {
        List<DropShelfItem> stagedItems = [];

        foreach (DropShelfItem item in items)
        {
            if (storageItems.ContainsKey(item.Path))
            {
                stagedItems.Add(item);
                continue;
            }

            try
            {
                IStorageItem storageItem = item.IsFolder
                    ? await StorageFolder.GetFolderFromPathAsync(item.Path)
                    : await StorageFile.GetFileFromPathAsync(item.Path);

                storageItems.TryAdd(item.Path, storageItem);
                stagedItems.Add(item);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(
                    $"DropShelf: cannot stage '{item.Path}': {exception}");
            }
        }

        return stagedItems;
    }

    public IReadOnlyList<IStorageItem> GetStorageItems(
        IEnumerable<DropShelfItem> items) =>
        items
            .Select(item => storageItems.TryGetValue(
                item.Path,
                out IStorageItem? storageItem)
                ? storageItem
                : null)
            .OfType<IStorageItem>()
            .ToArray();

    public void Remove(string path) => storageItems.TryRemove(path, out _);

    public void Clear() => storageItems.Clear();
}
