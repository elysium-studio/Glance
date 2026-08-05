namespace Glance.Application.Abstractions;

public interface IGlanceIntentService
{
    event EventHandler<GlanceIntentInvokedEventArgs>? IntentInvoked;

    IReadOnlyList<GlanceIntentDescriptor> GetIntents(GlanceContentKind kind);

    IReadOnlyList<GlanceIntentDescriptor> GetIntents(GlanceContentContext context);

    GlanceScreenRectangle? GetPresentationTarget();

    Task<bool> InvokeAsync(string intentId,
        GlanceContentContext context,
        CancellationToken cancellationToken = default);
}
