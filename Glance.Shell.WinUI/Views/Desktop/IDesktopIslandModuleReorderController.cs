using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandModuleReorderController
{
    void Attach(IDesktopIslandModuleReorderHost host);

    void Detach();

    void CenterSelected();

    void ListLoaded();

    void EdgeFadeHostSizeChanged();

    void PointerWheelChanged(PointerRoutedEventArgs args);

    void Previous();

    void Next();

    void ItemPointerEntered(object sender);

    void ItemPointerExited(object sender);

    void DragStarting(DragItemsStartingEventArgs args);

    void DragCompleted();

    void DragOver(DragEventArgs args);

    Task CreateDragVisualAsync(DragStartingEventArgs args);
}
