namespace Glance.Application.Abstractions;

public interface IGlanceContextAwareComponent
{
    bool CanHandle(GlanceContentKind kind);

    Task HandleAsync(GlanceContentContext context);
}
