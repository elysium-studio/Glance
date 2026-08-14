using Xunit;

namespace Glance.Spotify.Tests;

public sealed class SpotifyClientIdValidatorTests
{
    [Theory]
    [InlineData("0123456789abcdef")]
    [InlineData("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz")]
    public void AcceptsSpotifyClientIds(string value) =>
        Assert.True(SpotifyClientIdValidator.IsValid(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("0123456789abcde-")]
    [InlineData("0123456789abcde ")]
    public void RejectsInvalidClientIds(string? value) =>
        Assert.False(SpotifyClientIdValidator.IsValid(value));
}
