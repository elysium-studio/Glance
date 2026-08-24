namespace Glance.Application.Abstractions;

public interface IGlanceAttentionService
{
    event EventHandler<GlanceAttentionRequest>? AttentionRequested;

    void CompleteStartup();

    void RequestAttention(string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default);

    void RequestAttention(string componentId, GlanceAttentionLevel level, bool expand);

    void RequestExpandedAttention(string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default);
}
