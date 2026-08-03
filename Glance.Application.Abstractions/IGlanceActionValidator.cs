namespace Glance.Application.Abstractions;

public interface IGlanceActionValidator
{
    Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default);
}
