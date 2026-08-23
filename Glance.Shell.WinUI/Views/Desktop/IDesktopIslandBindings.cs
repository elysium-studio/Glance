using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandBindings
{
    DesktopIslandPlacement ToPlacement(int index);

    DesktopIslandHostMode ToHostMode(int index);

    DesktopIslandExpansionMode ToExpansionMode(int index);

    Visibility WhenPinned(bool isPinned);

    Visibility WhenNotPinned(bool isPinned);

    Visibility WhenOnScreenEdge(int index);

    Visibility WhenOnTaskbar(int index);

    Visibility WhenAvailable(bool isAvailable);

    Visibility WhenModulesLoaded(bool isLoadingModules);

    Visibility WhenModulesAvailable(bool isLoadingModules, int componentCount);

    Visibility WhenNoModulesAvailable(bool isLoadingModules, int componentCount);

    Visibility WhenRoutePickerVisible(bool isVisible);

    Visibility WhenRoutePickerHidden(bool isVisible);

    double ToCompactWidth(bool isAssistantAvailable, bool isAssistantEnabled, bool isLoadingModules, bool isTransientPresentationActive);

    Visibility WhenAssistantAvailable(bool isAssistantAvailable, bool isAssistantEnabled, bool isLoadingModules, bool isTransientPresentationActive);

    Visibility WhenPrimaryContentVisible(bool isLoadingModules, bool isTransientPresentationActive, int componentCount);

    object? ToBackgroundContent(IGlanceComponent? component, bool isLoadingModules, bool isTransientPresentationActive);
}
