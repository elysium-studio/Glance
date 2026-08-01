namespace Glance.Application.Abstractions;

public sealed record GlanceActionInvokedEventArgs(string TargetComponentId,
    GlanceActionPresentation Presentation,
    GlanceActionResult Result);
