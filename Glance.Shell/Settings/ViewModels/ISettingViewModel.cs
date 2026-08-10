using System.Collections;
using System.ComponentModel;

namespace Glance.Shell;

public interface ISettingViewModel :
    IEnumerable,
    IDisposable,
    INotifyPropertyChanged
{
    IReadOnlyList<ISettingViewModel> Children => [];

    string Glyph => string.Empty;

    string Title => string.Empty;
}
