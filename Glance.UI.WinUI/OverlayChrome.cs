using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Windows.UI;

namespace Glance.UI.WinUI;

public static class OverlayChrome
{
    public static Brush CreateAcrylicBrush()
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AcrylicInAppFillColorDefaultBrush", out object value))
        {
            if (value is AcrylicBrush acrylicBrush)
            {
                return acrylicBrush;
            }

            if (value is SolidColorBrush solidColorBrush)
            {
                return CreateAcrylicBrush(solidColorBrush.Color);
            }
        }

        return CreateAcrylicBrush(Color.FromArgb(245, 32, 32, 32));
    }

    public static void Elevate(Border border, float depth = 32)
    {
        border.Shadow = new ThemeShadow();
        ElementCompositionPreview.SetIsTranslationEnabled(border, true);
        border.Translation = new Vector3(0, 0, depth);
    }

    private static AcrylicBrush CreateAcrylicBrush(Color tintColor) =>
        new()
        {
            FallbackColor = tintColor,
            TintColor = tintColor,
            TintLuminosityOpacity = 0.82,
            TintOpacity = 0.72
        };
}
