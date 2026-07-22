using Glance.Application.Abstractions;
using Glance.Shell;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceAttentionServiceTests
{
    [Fact]
    public void RequestAttention_DuringStartup_IsDiscarded()
    {
        GlanceAttentionService service = new();
        int requestCount = 0;
        service.AttentionRequested += (_, _) => requestCount++;

        service.RequestAttention("DevicePresence");

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public void CompleteStartup_EnablesSubsequentAttention()
    {
        GlanceAttentionService service = new();
        GlanceAttentionRequest? request = null;
        service.AttentionRequested += (_, value) => request = value;
        service.CompleteStartup();

        service.RequestAttention("Timer", GlanceAttentionLevel.Critical, expand: false);

        Assert.Equal("Timer", request?.ComponentId);
        Assert.Equal(GlanceAttentionLevel.Critical, request?.Level);
        Assert.False(request?.Expand);
    }

    [Fact]
    public void SuppressedStartupAttention_IsNotReplayed()
    {
        GlanceAttentionService service = new();
        int requestCount = 0;
        service.AttentionRequested += (_, _) => requestCount++;
        service.RequestAttention("DevicePresence");

        service.CompleteStartup();

        Assert.Equal(0, requestCount);
    }
}
