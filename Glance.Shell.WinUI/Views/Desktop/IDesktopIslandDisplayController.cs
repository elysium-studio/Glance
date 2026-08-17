using Elysium.Platform.Abstractions;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandDisplayController
{
    DesktopIslandDisplayIcons GetIcons(WindowHandle window);
}
