using Elysium.Platform.Abstractions;
using Glance.Application.Abstractions;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandScreenTargetProvider
{
    GlanceScreenRectangle? GetTarget(WindowHandle window);
}
