using System.Collections;
using System.ComponentModel;

namespace Glance.Shell;

public interface ISettingViewModel :
    IEnumerable,
    IDisposable,
    INotifyPropertyChanged
{
    bool CanReorder => false;

    IReadOnlyList<ISettingViewModel> Children => [];

    string Glyph => string.Empty;

    string Title => string.Empty;

    Task SaveOrderAsync() => Task.CompletedTask;
}
