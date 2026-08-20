namespace Glance.Application.Abstractions;

public sealed record GlanceInspectorProviderExtension(string Id, string DisplayName, string Description, bool IsEnabled, bool CanRemove);
