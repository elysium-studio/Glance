namespace Glance.Presence.Tests;

public sealed class PresenceActivityPolicyTests
{
    private static readonly TimeSpan threshold = TimeSpan.FromMinutes(4);
    private readonly PresenceActivityPolicy policy = new(threshold);

    [Fact]
    public void ShouldSendInput_ReturnsFalseBeforeThreshold() => Assert.False(policy.ShouldSendInput(threshold - TimeSpan.FromMilliseconds(1)));

    [Fact]
    public void ShouldSendInput_ReturnsTrueAtThreshold() => Assert.True(policy.ShouldSendInput(threshold));

    [Fact]
    public void Constructor_RejectsNonPositiveThreshold() => _ = Assert.Throws<ArgumentOutOfRangeException>(() => new PresenceActivityPolicy(TimeSpan.Zero));
}
