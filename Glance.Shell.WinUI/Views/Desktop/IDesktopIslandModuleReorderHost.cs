using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandModuleReorderHost
{
    DispatcherQueue DispatcherQueue { get; }

    bool IsLoaded { get; }

    bool IsModuleReorderVisible { get; }

    IGlanceComponent? SelectedComponent { get; }

    IList<IGlanceComponent> ModuleOrder { get; }

    ListView ModuleReorderList { get; }

    FrameworkElement ModuleReorderListClipHost { get; }

    FrameworkElement ModuleReorderEdgeFadeHost { get; }

    Button PreviousModuleOrderButton { get; }

    Button NextModuleOrderButton { get; }

    double ModuleReorderItemWidth { get; }
}
