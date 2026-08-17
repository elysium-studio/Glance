using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Numerics;

namespace Glance.Shell.WinUI;

public interface IDesktopIslandAnimationHost
{
    DispatcherQueue DispatcherQueue { get; }

    DesktopIslandHostMode HostMode { get; }

    bool IsExpanded { get; }

    int SelectedIndex { get; }

    int ComponentCount { get; }

    IGlanceComponent? SelectedComponent { get; }

    FrameworkElement RootElement { get; }

    FrameworkElement CompactPresenter { get; }

    FrameworkElement ExpandedPresenter { get; }

    Vector3 GetTransitionOffset(bool isExpanded);

    TimeSpan GetTransitionDuration(bool isExpanded);

    CompositionEasingFunction CreateTransitionEasing();
}
