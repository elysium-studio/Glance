namespace Glance.Application.Abstractions;

public sealed record GlanceQuickConversionRequest(IReadOnlyList<GlanceStorageItem> Items,
    object? Options);
