using System.ComponentModel;

namespace Glance.Application.Abstractions;

public interface IGlanceAssistantService :
    INotifyPropertyChanged
{
    event EventHandler? WakeWordDetected;

    IReadOnlyList<IGlanceAssistantProvider> Providers { get; }

    IGlanceAssistantProvider? ActiveProvider { get; }

    bool IsAvailable { get; }

    bool IsEnabled { get; }

    bool IsOverlayVisible { get; }

    bool IsResultPresentationActive { get; }

    object? CompactIndicatorContent { get; }

    object? ExpandedIndicatorContent { get; }

    object? OverlayContent { get; }

    Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default);

    Task SetActiveProviderAsync(string providerId, CancellationToken cancellationToken = default);
}
