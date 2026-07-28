using System.Collections;
using System.ComponentModel;

namespace Glance.Shell;

public interface ISettingViewModel :
    IEnumerable,
    IDisposable,
    INotifyPropertyChanged
{
    string? Description => null;
}
