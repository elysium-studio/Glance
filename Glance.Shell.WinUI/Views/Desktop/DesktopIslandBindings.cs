using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;

namespace Glance.Shell.WinUI;

internal sealed class DesktopIslandBindings :
    IDesktopIslandBindings
{
    public DesktopIslandPlacement ToPlacement(int index) => ((GlancePlacement)index) switch
    {
        GlancePlacement.Top => DesktopIslandPlacement.Top,
        GlancePlacement.Bottom => DesktopIslandPlacement.Bottom,
        _ => DesktopIslandPlacement.Top
    };

    public DesktopIslandHostMode ToHostMode(int index) => (GlanceDisplayLocation)index == GlanceDisplayLocation.Taskbar ? DesktopIslandHostMode.Taskbar : DesktopIslandHostMode.Floating;

    public DesktopIslandExpansionMode ToExpansionMode(int index) => (GlanceExpansionMode)index == GlanceExpansionMode.ExpandOnClick ? DesktopIslandExpansionMode.ExpandOnClick : DesktopIslandExpansionMode.ExpandOnHover;

    public Visibility WhenPinned(bool isPinned) => isPinned ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenNotPinned(bool isPinned) => isPinned ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WhenOnScreenEdge(int index) => (GlanceDisplayLocation)index == GlanceDisplayLocation.DesktopEdge ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenOnTaskbar(int index) => (GlanceDisplayLocation)index == GlanceDisplayLocation.Taskbar ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenAvailable(bool isAvailable) => isAvailable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenModulesLoaded(bool isLoadingModules) => isLoadingModules ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WhenRoutePickerVisible(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenRoutePickerHidden(bool isVisible) => isVisible ? Visibility.Collapsed : Visibility.Visible;

    public double ToCompactWidth(bool isAssistantAvailable, bool isAssistantEnabled, bool isLoadingModules, bool isTransientPresentationActive) => !isLoadingModules && !isTransientPresentationActive && isAssistantAvailable && isAssistantEnabled ? 268 : 228;

    public Visibility WhenAssistantAvailable(bool isAssistantAvailable, bool isAssistantEnabled, bool isLoadingModules, bool isTransientPresentationActive) => !isLoadingModules && !isTransientPresentationActive && isAssistantAvailable && isAssistantEnabled ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenPrimaryContentVisible(bool isLoadingModules, bool isTransientPresentationActive) => isLoadingModules || isTransientPresentationActive ? Visibility.Collapsed : Visibility.Visible;

    public object? ToBackgroundContent(IGlanceComponent? component, bool isLoadingModules, bool isTransientPresentationActive) => isLoadingModules || isTransientPresentationActive ? null : (component as IGlanceBackgroundComponent)?.BackgroundContent;
}
