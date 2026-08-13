using Glance.SystemIndicators;

namespace Glance.SystemIndicators.Tests;

public sealed class SystemIndicatorStateTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(47, 47)]
    [InlineData(100, 100)]
    [InlineData(130, 100)]
    public void NormalizedLevelClampsPercentage(int value,
        int expected)
    {
        SystemIndicatorState state = new(SystemIndicatorKind.Volume, value);

        Assert.Equal(expected, state.NormalizedLevel);
    }

    [Fact]
    public void ToggleStateDoesNotInventALevel()
    {
        SystemIndicatorState state = new(SystemIndicatorKind.CapsLock,
            IsEnabled: true);

        Assert.Null(state.NormalizedLevel);
        Assert.True(state.IsEnabled);
    }
}
