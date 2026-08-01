namespace Glance.Application.Abstractions;

public sealed record GlanceActionParameterDescriptor(string Name,
    GlanceActionParameterType Type,
    string Description,
    bool IsRequired = true,
    IReadOnlyList<string>? AllowedValues = null,
    double? Minimum = null,
    double? Maximum = null);
