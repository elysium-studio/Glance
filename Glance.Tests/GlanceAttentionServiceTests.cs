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

        service.RequestAttention("Timer", GlanceAttentionLevel.Critical);

        Assert.Equal("Timer", request?.ComponentId);
        Assert.Equal(GlanceAttentionLevel.Critical, request?.Level);
        Assert.False(request?.Expand);
    }

    [Fact]
    public void RequestAttention_DefaultsToCompactPresentation()
    {
        GlanceAttentionService service = new();
        GlanceAttentionRequest? request = null;
        service.AttentionRequested += (_, value) => request = value;
        service.CompleteStartup();

        service.RequestAttention("Hydration", GlanceAttentionLevel.Critical);

        Assert.Equal("Hydration", request?.ComponentId);
        Assert.Equal(GlanceAttentionLevel.Critical, request?.Level);
        Assert.False(request?.Expand);
    }

    [Fact]
    public void RequestExpandedAttention_UsesExpandedPresentation()
    {
        GlanceAttentionService service = new();
        GlanceAttentionRequest? request = null;
        service.AttentionRequested += (_, value) => request = value;
        service.CompleteStartup();

        service.RequestExpandedAttention("Interactive");

        Assert.True(request?.Expand);
    }

    [Fact]
    public void LegacyExpandedAttention_UsesCompactPresentation()
    {
        GlanceAttentionService service = new();
        GlanceAttentionRequest? request = null;
        service.AttentionRequested += (_, value) => request = value;
        service.CompleteStartup();

        service.RequestAttention("Hydration", GlanceAttentionLevel.Default, true);

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
