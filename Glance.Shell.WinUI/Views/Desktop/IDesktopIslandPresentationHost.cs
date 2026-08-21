using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandPresentationHost
{
    DispatcherQueue DispatcherQueue { get; }

    DesktopIslandViewModel ViewModel { get; }

    IDesktopIslandBindings BindingPolicy { get; }

    bool IsLoaded { get; }

    bool StaysExpanded { get; set; }

    bool DismissesOnOutsideClick { get; set; }

    object? BackgroundContent { get; set; }

    FrameworkElement CompactPresenter { get; }

    FrameworkElement ExpandedPresenter { get; }

    FrameworkElement TransientCompactPresenter { get; }

    FrameworkElement TransientExpandedPresenter { get; }

    FrameworkElement CompactModuleLoadingView { get; }

    FrameworkElement ExpandedModuleLoadingView { get; }

    ContentControl CompactAssistantIndicator { get; }

    ContentControl ExpandedAssistantIndicator { get; }

    FrameworkElement Footer { get; }

    FrameworkElement ExpandedModuleSurface { get; }

    ContentControl AssistantOverlayPresenter { get; }

    FrameworkElement ContentRoutePicker { get; }

    FrameworkElement ModuleReorderSurface { get; }

    FrameworkElement ExpandedContentHost { get; }

    FrameworkElement? ContentTransitionClipHost { get; }

    FrameworkElement? BackgroundElement { get; }

    FrameworkElement? CompactTemplateContent { get; }

    object? GetModuleBackgroundContent();

    void UpdateLayout();

    void Reveal();

    void Dismiss();
}
