using Glance.Application.Abstractions;

namespace Glance.QuickConvert.WinUI;

internal sealed record QuickConversionJob(IGlanceQuickConverter Converter,
    IReadOnlyList<GlanceStorageItem> Items,
    object? Options,
    long Generation,
    CancellationToken CancellationToken);
