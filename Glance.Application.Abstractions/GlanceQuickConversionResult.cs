namespace Glance.Application.Abstractions;

public sealed record GlanceQuickConversionResult(string SourcePath,
    string? OutputPath,
    bool IsSuccessful,
    string? ErrorMessage = null);
