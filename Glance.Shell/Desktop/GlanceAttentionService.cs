using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceAttentionService :
    IGlanceAttentionService
{
    private bool isStartupComplete;

    public event EventHandler<GlanceAttentionRequest>? AttentionRequested;

    public void CompleteStartup() => isStartupComplete = true;

    public void RequestAttention(string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default) => Publish(componentId, level, false);

    public void RequestAttention(string componentId, GlanceAttentionLevel level, bool expand) => Publish(componentId, level, false);

    public void RequestExpandedAttention(string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default) => Publish(componentId, level, true);

    private void Publish(string componentId, GlanceAttentionLevel level, bool expand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);

        if (!isStartupComplete)
        {
            return;
        }

        AttentionRequested?.Invoke(this, new GlanceAttentionRequest(componentId, level, expand));
    }
}
