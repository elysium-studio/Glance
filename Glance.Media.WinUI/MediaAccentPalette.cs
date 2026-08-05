using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Glance.Media.WinUI;

internal static class MediaAccentPalette
{
    public static Color GetAccent(uint value) => FromArgb(value);

    public static Color GetPointerOver(uint value) => AdjustLightness(FromArgb(value), 0.08);

    public static Color GetPressed(uint value) => AdjustLightness(FromArgb(value), -0.08);

    public static Color GetDisabled(uint value)
    {
        Color color = FromArgb(value);
        return Color.FromArgb(72, color.R, color.G, color.B);
    }

    public static Color GetForeground(uint value) => GetContrastingForeground(GetAccent(value), 255);

    public static Color GetPointerOverForeground(uint value) => GetForeground(value);

    public static Color GetPressedForeground(uint value) => GetForeground(value);

    public static Color GetDisabledForeground(uint value) => GetForeground(value);

    public static Color GetBorder(uint value) => WithAlpha(GetForeground(value), 44);

    public static Color GetPointerOverBorder(uint value) => WithAlpha(GetPointerOverForeground(value), 58);

    public static Color GetPressedBorder(uint value) => WithAlpha(GetPressedForeground(value), 30);

    public static Color GetDisabledBorder(uint value) => WithAlpha(GetDisabledForeground(value), 18);

    public static SolidColorBrush GetBrush(uint value) => new(GetAccent(value));

    private static Color FromArgb(uint value) => Color.FromArgb((byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value);

    private static Color AdjustLightness(Color color, double amount)
    {
        double red = color.R / 255d;
        double green = color.G / 255d;
        double blue = color.B / 255d;
        double maximum = Math.Max(red, Math.Max(green, blue));
        double minimum = Math.Min(red, Math.Min(green, blue));
        double lightness = (maximum + minimum) / 2;
        double scale = lightness is 0 or 1 ? 0 :
            amount / (amount > 0 ? 1 - lightness : lightness);
        return Color.FromArgb(color.A,
            ToByte(red + ((amount > 0 ? 1 - red : red) * scale)),
            ToByte(green + ((amount > 0 ? 1 - green : green) * scale)),
            ToByte(blue + ((amount > 0 ? 1 - blue : blue) * scale)));
    }

    private static Color GetContrastingForeground(Color background, byte alpha)
    {
        double luminance = RelativeLuminance(background);
        double whiteContrast = 1.05 / (luminance + 0.05);
        double blackContrast = (luminance + 0.05) / 0.05;
        return whiteContrast >= blackContrast ?
            Color.FromArgb(alpha, 255, 255, 255) :
            Color.FromArgb(alpha, 0, 0, 0);
    }

    private static double RelativeLuminance(Color color) => (0.2126 * Linearize(color.R)) +
        (0.7152 * Linearize(color.G)) +
        (0.0722 * Linearize(color.B));

    private static double Linearize(byte channel)
    {
        double value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}
