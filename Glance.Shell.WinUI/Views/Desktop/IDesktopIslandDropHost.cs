using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandDropHost
{
    DispatcherQueue DispatcherQueue { get; }

    DesktopIslandHostMode HostMode { get; }

    bool IsModuleReorderVisible { get; }

    bool IsPinned { get; }

    bool IsExpanded { get; }

    bool CanHandleContent(GlanceContentKind kind);

    bool TryActivateContent(GlanceContentContext context, bool restoreExpandedState);

    bool TryActivateContentRoute(string routeId);

    Task<bool> HandleContentAsync(GlanceContentContext context);

    void CompleteContentDrop();

    void CancelContentDrop();
}
