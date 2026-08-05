namespace Glance.Application.Abstractions;

public interface IGlanceContentHandlingResultComponent
{
    Task<bool> TryHandleAsync(GlanceContentContext context);
}
