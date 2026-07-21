using System.Globalization;

namespace Glance.ColorPicker;

public readonly record struct ColorValue(
    byte Red,
    byte Green,
    byte Blue)
{
    public string Hex => $"#{Red:X2}{Green:X2}{Blue:X2}";

    public string Rgb => $"rgb({Red}, {Green}, {Blue})";

    public string Hsl
    {
        get
        {
            (double hue, double saturation, double lightness) = ToHsl();

            return string.Create(
                CultureInfo.InvariantCulture,
                $"hsl({Math.Round(hue)}, {Math.Round(saturation * 100)}%, {Math.Round(lightness * 100)}%)");
        }
    }

    public bool UsesLightForeground =>
        ((0.2126 * Red) + (0.7152 * Green) + (0.0722 * Blue)) < 145;

    private (double Hue, double Saturation, double Lightness) ToHsl()
    {
        double red = Red / 255d;
        double green = Green / 255d;
        double blue = Blue / 255d;
        double maximum = Math.Max(red, Math.Max(green, blue));
        double minimum = Math.Min(red, Math.Min(green, blue));
        double difference = maximum - minimum;
        double lightness = (maximum + minimum) / 2d;

        if (difference == 0)
        {
            return (0, 0, lightness);
        }

        double saturation = difference / (1d - Math.Abs((2d * lightness) - 1d));
        double hue = maximum == red
            ? 60d * (((green - blue) / difference) % 6d)
            : maximum == green
                ? 60d * (((blue - red) / difference) + 2d)
                : 60d * (((red - green) / difference) + 4d);

        return (hue < 0 ? hue + 360d : hue, saturation, lightness);
    }
}
