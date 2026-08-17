using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandComponentHost
{
    DispatcherQueue DispatcherQueue { get; }

    FrameworkElement RootElement { get; }

    ContentControl CompactAssistantIndicator { get; }

    IGlanceComponent? SelectedComponent { get; }

    bool IsSelectedComponentVisible { get; }

    bool IsPinned { get; }

    bool IsModuleReorderVisible { get; }

    bool IsTransientExpansionLocked { get; }

    bool IsPointerWithinInteractiveRegion { get; }

    void SetAllowsActivation(bool value);

    void SetExpansionLocked(bool value);
}
