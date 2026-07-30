namespace Glance.Application.Abstractions;

public interface IGlanceIntent
{
    GlanceIntentDescriptor Descriptor { get; }

    bool CanHandle(GlanceContentKind kind);

    Task InvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken = default);
}
