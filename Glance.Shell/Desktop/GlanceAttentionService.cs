using Glance.Application.Abstractions;

namespace Glance.Shell;

public sealed class GlanceAttentionService : IGlanceAttentionService
{
    private bool isStartupComplete;

    public event EventHandler<GlanceAttentionRequest>? AttentionRequested;

    public void CompleteStartup() =>
        isStartupComplete = true;

    public void RequestAttention(
        string componentId,
        GlanceAttentionLevel level = GlanceAttentionLevel.Default,
        bool expand = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);

        if (!isStartupComplete)
        {
            return;
        }

        AttentionRequested?.Invoke(this, new GlanceAttentionRequest(componentId, level, expand));
    }
}
