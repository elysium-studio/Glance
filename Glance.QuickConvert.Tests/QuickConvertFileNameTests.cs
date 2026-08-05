namespace Glance.QuickConvert.Tests;

public sealed class QuickConvertFileNameTests
{
    [Fact]
    public void SameFormatNeverOverwritesTheOriginal()
    {
        string source = Path.Combine("images", "photo.png");

        string output = QuickConvertFileName.Create(source, "png", path => path == source);

        Assert.Equal(Path.Combine("images", "photo converted.png"), output);
    }
}
