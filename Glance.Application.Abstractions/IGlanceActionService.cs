namespace Glance.Application.Abstractions;

public interface IGlanceActionService
{
    event EventHandler<GlanceActionPresentationRequestedEventArgs>? PresentationRequested;

    event EventHandler<GlanceActionInvokedEventArgs>? ActionInvoked;

    IReadOnlyList<GlanceActionDescriptor> GetActions();

    GlanceActionDescriptor? GetAction(string actionId);

    Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default);
}
