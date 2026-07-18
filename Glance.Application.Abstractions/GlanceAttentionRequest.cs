namespace Glance.Application.Abstractions;

public sealed record GlanceAttentionRequest(
    string ComponentId,
    GlanceAttentionLevel Level = GlanceAttentionLevel.Default,
    bool Expand = true);
