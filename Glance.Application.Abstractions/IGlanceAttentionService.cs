namespace Glance.Application.Abstractions;

public interface IGlanceAttentionService
{
    event EventHandler<GlanceAttentionRequest>? AttentionRequested;

    void RequestAttention(
        string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default,
        bool expand = true);
}
