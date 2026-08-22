namespace Glance.Application.Abstractions;

public sealed record GlanceModuleCategoryDescriptor(string Id, string DisplayName, string Glyph, int Order = 500);
