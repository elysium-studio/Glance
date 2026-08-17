namespace Glance.Application.Abstractions;

public interface IGlanceIntent
{
    GlanceIntentDescriptor Descriptor { get; }

    bool CanHandle(GlanceContentKind kind);

    bool CanHandle(GlanceContentContext context) => CanHandle(context.Kind);

    Task InvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken = default);

    async Task<bool> TryInvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken = default)
    {
        await InvokeAsync(context, cancellationToken);
        return true;
    }
}
