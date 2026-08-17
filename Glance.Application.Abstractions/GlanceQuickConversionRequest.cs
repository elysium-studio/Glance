namespace Glance.Application.Abstractions;

public sealed record GlanceQuickConversionRequest(GlanceContentContext Content,
    object? Options,
    IProgress<GlanceQuickConversionProgress>? Progress = null);
