namespace Glance.QuickConvert.Tests;

public sealed class ImageDimensionCalculatorTests
{
    [Fact]
    public void PercentagePreservesAspectRatio()
    {
        ImageConversionOptions options = new("png", ImageScaleMode.Percentage, 50, 0, 0, 0.9);

        Assert.Equal((800u, 450u), ImageDimensionCalculator.Calculate(1600, 900, options));
    }

    [Fact]
    public void FitWithinDoesNotUpscale()
    {
        ImageConversionOptions options = new("jpeg", ImageScaleMode.FitWithin, 100, 1920, 1080, 0.9);

        Assert.Equal((800u, 600u), ImageDimensionCalculator.Calculate(800, 600, options));
    }

    [Fact]
    public void FitWithinUsesTheTighterEdge()
    {
        ImageConversionOptions options = new("webp", ImageScaleMode.FitWithin, 100, 1000, 1000, 0.9);

        Assert.Equal((1000u, 500u), ImageDimensionCalculator.Calculate(2000, 1000, options));
    }
}
