using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandDropController
{
    bool IsActive { get; }

    void Attach(IDesktopIslandDropHost host);

    void Detach();

    Task EnterAsync(DragEventArgs args);

    void Over(DragEventArgs args);

    void Leave();

    void EnterRoute(object sender, DragEventArgs args);

    void OverRoute(DragEventArgs args);

    void OverRoutePicker(DragEventArgs args);

    void LeaveRoute(object sender);

    void LeaveRoutePicker();

    void DropOnRoute(object sender, DragEventArgs args);

    void ReleaseActiveRouteTarget();

    void ResetRoutePicker();

    Task DropAsync(DragEventArgs args);
}
