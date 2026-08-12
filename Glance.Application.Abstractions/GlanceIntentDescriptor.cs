namespace Glance.Application.Abstractions;

public sealed record GlanceIntentDescriptor(string Id,
    string TargetComponentId,
    string DisplayName,
    string Description,
    string Glyph,
    string GlyphFontFamily = "Segoe Fluent Icons");
