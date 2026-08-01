namespace Glance.Application.Abstractions;

public interface IGlanceActionProvider
{
    IReadOnlyList<GlanceActionDescriptor> GetActions();

    bool IsAvailable(string actionId) => true;

    Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default);
}
