using Glance.Application.Abstractions;

namespace Glance.Shell;

internal sealed record GlanceQuickConverterRegistration(IGlanceQuickConverter Converter, string? PackageId);
