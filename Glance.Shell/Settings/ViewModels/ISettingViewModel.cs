using System.Collections;
using System.ComponentModel;

namespace Glance.Shell;

public interface ISettingViewModel :
    IEnumerable,
    IDisposable,
    INotifyPropertyChanged
{
    bool CanReorder => false;

    string? Description => null;
}
