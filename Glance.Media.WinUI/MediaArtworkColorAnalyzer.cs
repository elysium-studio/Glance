using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Glance.Media.WinUI;

internal static class MediaArtworkColorAnalyzer
{
    private const uint SampleSize = 32;

    public static async Task<MediaArtworkColors> AnalyzeAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        BitmapTransform transform = new()
        {
            ScaledWidth = SampleSize,
            ScaledHeight = SampleSize,
            InterpolationMode = BitmapInterpolationMode.Fant
        };
        PixelDataProvider provider = await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Straight, transform, ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        byte[] pixels = provider.DetachPixelData();
        Dictionary<int, ColorBucket> buckets = [];
        double totalRed = 0;
        double totalGreen = 0;
        double totalBlue = 0;
        int pixelCount = 0;

        for (int index = 0; index + 3 < pixels.Length; index += 4)
        {
            byte red = pixels[index];
            byte green = pixels[index + 1];
            byte blue = pixels[index + 2];
            byte alpha = pixels[index + 3];

            if (alpha < 128)
            {
                continue;
            }

            totalRed += red;
            totalGreen += green;
            totalBlue += blue;
            pixelCount++;

            double maximum = Math.Max(red, Math.Max(green, blue)) / 255d;
            double minimum = Math.Min(red, Math.Min(green, blue)) / 255d;
            double lightness = (maximum + minimum) / 2;

            if (lightness is < 0.06 or > 0.94)
            {
                continue;
            }

            double saturation = maximum == minimum ? 0 :
                (maximum - minimum) / (1 - Math.Abs((2 * lightness) - 1));
            double weight = 0.3 + (saturation * 1.7);
            int key = ((red >> 4) << 8) | ((green >> 4) << 4) | (blue >> 4);
            _ = buckets.TryGetValue(key, out ColorBucket bucket);
            buckets[key] = bucket.Add(red, green, blue, weight);
        }

        ColorBucket dominant = buckets.Values.OrderByDescending(bucket => bucket.Score).FirstOrDefault();

        if (dominant.Count == 0)
        {
            return new MediaArtworkColors(MediaViewModel.DefaultAccentColor, 0xFFFFFFFF);
        }

        uint accentColor = Normalize(dominant.Red / dominant.Count,
            dominant.Green / dominant.Count,
            dominant.Blue / dominant.Count);
        uint averageColor = pixelCount == 0 ? accentColor :
            ToColor(totalRed / pixelCount, totalGreen / pixelCount, totalBlue / pixelCount);
        Windows.UI.Color foreground = MediaAccentPalette.GetForeground(averageColor);
        uint foregroundColor = ((uint)foreground.A << 24) |
            ((uint)foreground.R << 16) |
            ((uint)foreground.G << 8) |
            foreground.B;
        return new MediaArtworkColors(accentColor, foregroundColor);
    }

    private static uint ToColor(double red, double green, double blue) => 0xFF000000u |
        ((uint)Math.Round(red) << 16) |
        ((uint)Math.Round(green) << 8) |
        (uint)Math.Round(blue);

    private static uint Normalize(double red, double green, double blue)
    {
        (double hue, double saturation, double lightness) = ToHsl(red / 255, green / 255, blue / 255);
        saturation = Math.Clamp(Math.Max(saturation, 0.5), 0, 0.82);
        lightness = Math.Clamp(lightness, 0.46, 0.66);
        (byte normalizedRed, byte normalizedGreen, byte normalizedBlue) = FromHsl(hue, saturation, lightness);
        return 0xFF000000u |
            ((uint)normalizedRed << 16) |
            ((uint)normalizedGreen << 8) |
            normalizedBlue;
    }

    private static (double Hue, double Saturation, double Lightness) ToHsl(double red, double green, double blue)
    {
        double maximum = Math.Max(red, Math.Max(green, blue));
        double minimum = Math.Min(red, Math.Min(green, blue));
        double delta = maximum - minimum;
        double lightness = (maximum + minimum) / 2;

        if (delta == 0)
        {
            return (0, 0, lightness);
        }

        double saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
        double hue = maximum == red ? (green - blue) / delta % 6 :
            maximum == green ? ((blue - red) / delta) + 2 :
            ((red - green) / delta) + 4;
        hue = ((hue * 60) + 360) % 360;
        return (hue, saturation, lightness);
    }

    private static (byte Red, byte Green, byte Blue) FromHsl(double hue, double saturation, double lightness)
    {
        double chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        double section = hue / 60;
        double secondary = chroma * (1 - Math.Abs((section % 2) - 1));
        (double red, double green, double blue) = section switch
        {
            < 1 => (chroma, secondary, 0d),
            < 2 => (secondary, chroma, 0d),
            < 3 => (0d, chroma, secondary),
            < 4 => (0d, secondary, chroma),
            < 5 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };
        double match = lightness - (chroma / 2);
        return ((byte)Math.Round((red + match) * 255),
            (byte)Math.Round((green + match) * 255),
            (byte)Math.Round((blue + match) * 255));
    }

    private readonly record struct ColorBucket(double Red,
        double Green,
        double Blue,
        double Score,
        int Count)
    {
        public ColorBucket Add(byte red, byte green, byte blue, double weight) => new(Red + red, Green + green, Blue + blue, Score + weight, Count + 1);
    }
}

internal readonly record struct MediaArtworkColors(uint AccentColor,
    uint ForegroundColor);
