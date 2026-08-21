namespace Glance.Application.Abstractions;

public sealed record GlanceInspectionSection(string Title, IReadOnlyList<GlanceInspectionProperty> Properties, GlanceInspectionDistribution? Distribution = null);
