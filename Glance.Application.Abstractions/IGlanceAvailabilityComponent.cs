namespace Glance.Application.Abstractions;

public interface IGlanceAvailabilityComponent
{
    bool IsAvailable { get; }

    event EventHandler? AvailabilityChanged;
}
