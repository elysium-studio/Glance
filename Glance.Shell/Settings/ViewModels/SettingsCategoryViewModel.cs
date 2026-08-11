using System.Collections.ObjectModel;

namespace Glance.Shell;

public class SettingsCategoryViewModel :
    ObservableCollection<object>,
    ISettingViewModel
{
    private bool disposed;

    public SettingsCategoryViewModel(string id,
        string title,
        string glyph,
        IEnumerable<object> items) :
        base(items)
    {
        Id = id;
        Title = title;
        Glyph = glyph;
    }

    public string Glyph { get; }

    public string Id { get; }

    public string Title { get; }

    public virtual void Dispose()
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
