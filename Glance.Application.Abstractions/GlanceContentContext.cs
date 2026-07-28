namespace Glance.Application.Abstractions;

public sealed record GlanceContentContext(GlanceContentKind Kind,
    IReadOnlyList<GlanceStorageItem> StorageItems,
    string? Content = null);
