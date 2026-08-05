using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Glance.Shell.WinUI;

public sealed partial class GlanceIntentAccentBrushConverter :
    IValueConverter
{
    public object Convert(object value,
        Type targetType,
        object parameter,
        string language)
    {
        if (value is not GlanceContentRoute route)
        {
            return ResolveDefaultBrush();
        }

        return route.AccentResourceSource is FrameworkElement source &&
            source.Resources.TryGetValue(route.AccentResourceKey, out object resource) &&
            resource is Brush brush
            ? brush
            : ResolveDefaultBrush();
    }

    public object ConvertBack(object value,
        Type targetType,
        object parameter,
        string language) => throw new NotSupportedException();

    private static Brush ResolveDefaultBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush",
            out object resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(255, 96, 205, 255));
}
