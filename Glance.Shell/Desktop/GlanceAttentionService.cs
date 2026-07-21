using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceAttentionService : IGlanceAttentionService
{
    public event EventHandler<GlanceAttentionRequest>? AttentionRequested;

    public void RequestAttention(
        string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default,
        bool expand = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);

        AttentionRequested?.Invoke(this, new GlanceAttentionRequest(componentId, level, expand));
    }
}
