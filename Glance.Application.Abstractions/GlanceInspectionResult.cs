namespace Glance.Application.Abstractions;

public sealed record GlanceInspectionResult(IReadOnlyList<GlanceInspectionSection> Sections, IReadOnlyList<IGlanceInspectionAction> Actions)
{
    public static GlanceInspectionResult Empty { get; } = new([], []);
}
