using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Globalization;
using Windows.Foundation;
using Windows.UI;

namespace Glance.Shell.WinUI;

internal static class GlanceModuleFeedIconFactory
{
    public static IconElement? Create(GlanceModuleFeedIcon? icon, bool isLightTheme, Brush foreground, double size)
    {
        if (icon is null)
        {
            return null;
        }

        string source = isLightTheme && !string.IsNullOrWhiteSpace(icon.LightSource) ? icon.LightSource : icon.Source;

        if (icon.Type == GlanceModuleIconType.Glyph)
        {
            return new FontIcon
            {
                FontFamily = new FontFamily(string.IsNullOrWhiteSpace(icon.FontFamily) ? "Segoe Fluent Icons" : icon.FontFamily),
                FontSize = size,
                Foreground = foreground,
                Glyph = source
            };
        }

        if (icon.Type == GlanceModuleIconType.Bitmap)
        {
            return new ImageIcon
            {
                Width = size,
                Height = size,
                Source = new BitmapImage(new Uri(source))
            };
        }

        try
        {
            Geometry geometry = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), source);
            Rect bounds = geometry.Bounds;

            if (bounds.Width > 0 && bounds.Height > 0)
            {
                double scale = Math.Min(size / bounds.Width, size / bounds.Height);
                geometry.Transform = new CompositeTransform
                {
                    ScaleX = scale,
                    ScaleY = scale,
                    TranslateX = ((size - bounds.Width * scale) / 2) - bounds.X * scale,
                    TranslateY = ((size - bounds.Height * scale) / 2) - bounds.Y * scale
                };
            }

            return new PathIcon
            {
                Width = size,
                Height = size,
                Data = geometry,
                Foreground = foreground
            };
        }
        catch
        {
            return null;
        }
    }

    public static Brush? CreateAccentBrush(GlanceModuleFeedIcon? icon, bool isLightTheme)
    {
        string colorValue = isLightTheme ? icon?.LightAccentColor ?? string.Empty : icon?.AccentColor ?? string.Empty;
        string hex = colorValue.Trim().TrimStart('#');

        if ((hex.Length != 6 && hex.Length != 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
        {
            return null;
        }

        Color color = hex.Length == 6 ? Color.FromArgb(255, (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed) : Color.FromArgb((byte)(parsed >> 24), (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed);
        return new SolidColorBrush(color);
    }
}
