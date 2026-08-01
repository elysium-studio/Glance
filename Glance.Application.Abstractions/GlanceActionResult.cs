namespace Glance.Application.Abstractions;

public sealed record GlanceActionResult(GlanceActionStatus Status,
    string? Message = null)
{
    public bool Succeeded => Status == GlanceActionStatus.Succeeded;

    public static GlanceActionResult Success(string? message = null) =>
        new(GlanceActionStatus.Succeeded, message);

    public static GlanceActionResult InvalidArguments(string? message = null) =>
        new(GlanceActionStatus.InvalidArguments, message);

    public static GlanceActionResult Unavailable(string? message = null) =>
        new(GlanceActionStatus.Unavailable, message);

    public static GlanceActionResult Failed(string? message = null) =>
        new(GlanceActionStatus.Failed, message);
}
