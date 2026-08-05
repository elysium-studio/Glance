namespace Glance.Application.Abstractions;

public interface IGlanceContextAwareComponent
{
    bool CanHandle(GlanceContentKind kind);

    bool CanHandle(GlanceContentContext context) => CanHandle(context.Kind);

    void BeginContentPreview(GlanceContentContext context)
    {
    }

    void EndContentPreview()
    {
    }

    Task HandleAsync(GlanceContentContext context);
}
