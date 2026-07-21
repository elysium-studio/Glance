namespace Glance.ColorPicker.Tests;

public sealed class ColorValueTests
{
    [Fact]
    public void FormatsColorValues()
    {
        ColorValue color = new(2, 14, 30);

        Assert.Equal("#020E1E", color.Hex);
        Assert.Equal("rgb(2, 14, 30)", color.Rgb);
        Assert.Equal("hsl(214, 88%, 6%)", color.Hsl);
    }

    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(255, 255, 255, false)]
    [InlineData(0, 120, 212, true)]
    public void ChoosesReadableForeground(
        byte red,
        byte green,
        byte blue,
        bool expected)
    {
        ColorValue color = new(red, green, blue);

        Assert.Equal(expected, color.UsesLightForeground);
    }
}
