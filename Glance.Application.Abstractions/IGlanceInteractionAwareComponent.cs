namespace Glance.Application.Abstractions;

public interface IGlanceInteractionAwareComponent
{
    void BeginInteraction();

    void EndInteraction();
}
