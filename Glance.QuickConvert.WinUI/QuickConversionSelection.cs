using Glance.Application.Abstractions;

namespace Glance.QuickConvert.WinUI;

internal sealed record QuickConversionSelection(IGlanceQuickConverter Converter,
    object? Options);
