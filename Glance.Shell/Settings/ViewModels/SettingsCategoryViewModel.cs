using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed class SettingsCategoryViewModel :
    ObservableCollection<object>,
    ISettingViewModel
{
    private readonly Func<IEnumerable<object>, Task>? saveOrder;
    private bool disposed;

    public SettingsCategoryViewModel(string id,
        string title,
        string glyph,
        IEnumerable<object> items,
        Func<IEnumerable<object>, Task>? saveOrder = null) :
        base(items)
    {
        Id = id;
        Title = title;
        Glyph = glyph;
        this.saveOrder = saveOrder;
    }

    public bool CanReorder => saveOrder is not null;

    public string Glyph { get; }

    public string Id { get; }

    public string Title { get; }

    public async Task SaveOrderAsync()
    {
        if (saveOrder is not null)
        {
            await saveOrder(this);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (IDisposable item in this.OfType<IDisposable>())
        {
            item.Dispose();
        }

        Clear();
        GC.SuppressFinalize(this);
    }
}
