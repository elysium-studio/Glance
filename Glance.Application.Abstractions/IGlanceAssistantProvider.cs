using System.ComponentModel;

namespace Glance.Application.Abstractions;

public interface IGlanceAssistantProvider :
    INotifyPropertyChanged
{
    string Id { get; }

    string DisplayName { get; }

    GlanceAssistantState State { get; }

    string StatusText { get; }

    string Transcript { get; }

    object CompactIndicatorContent { get; }

    object ExpandedIndicatorContent { get; }

    object OverlayContent { get; }

    Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default);
}
