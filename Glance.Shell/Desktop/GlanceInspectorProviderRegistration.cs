using Glance.Application.Abstractions;

namespace Glance.Shell;

internal sealed record GlanceInspectorProviderRegistration(IGlanceInspectorProvider Provider, string? PackageId);
