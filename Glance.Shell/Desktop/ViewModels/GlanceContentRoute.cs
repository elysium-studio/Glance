using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed record GlanceContentRoute(GlanceIntentDescriptor Intent,
    IGlanceComponent TargetComponent)
{
    public string Id => Intent.Id;

    public string TargetComponentId => Intent.TargetComponentId;

    public string DisplayName => Intent.DisplayName;

    public string Description => Intent.Description;

    public string Glyph => Intent.Glyph;

    public string GlyphFontFamily => Intent.GlyphFontFamily;

    public string AccentResourceKey => TargetComponent.AccentResourceKey;

    public object AccentResourceSource => TargetComponent.CompactContent;
}
