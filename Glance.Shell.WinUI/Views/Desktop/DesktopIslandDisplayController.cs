using Elysium.Platform.Abstractions;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandDisplayController :
    IDesktopIslandDisplayController
{
    private readonly IMonitorLocator monitorLocator;
    private readonly ITaskbarLocator taskbarLocator;

    public DesktopIslandDisplayController(IMonitorLocator monitorLocator, ITaskbarLocator taskbarLocator)
    {
        this.monitorLocator = monitorLocator;
        this.taskbarLocator = taskbarLocator;
    }

    public DesktopIslandDisplayIcons GetIcons(WindowHandle window)
    {
        MonitorHandle monitor = monitorLocator.GetMonitorForWindow(window);
        bool isTaskbarAtTop = taskbarLocator.GetTaskbarForMonitor(monitor)?.Edge == TaskbarEdge.Top;
        return new DesktopIslandDisplayIcons(isTaskbarAtTop ? "\uE8AD" : "\uEA4F", isTaskbarAtTop ? 0 : 180, isTaskbarAtTop ? "\uE8AD" : "\uEA4F", isTaskbarAtTop ? 180 : 0);
    }
}
