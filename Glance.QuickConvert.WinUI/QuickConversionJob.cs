using Glance.Application.Abstractions;

namespace Glance.QuickConvert.WinUI;

internal sealed record QuickConversionJob(IGlanceQuickConverter Converter,
    GlanceContentContext Content,
    object? Options,
    long Generation,
    CancellationToken CancellationToken);
