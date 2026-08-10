using Glance.Application.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace Glance.Shell.WinUI;

public sealed partial class GlanceModuleAccentBrushConverter :
    IValueConverter
{
    public object Convert(object value,
        Type targetType,
        object parameter,
        string language)
    {
        (string resourceKey, object? resourceSource) = value switch
        {
            IGlanceComponent component => (component.AccentResourceKey, component.CompactContent),
            ModuleSettingsItemViewModel module => (module.AccentResourceKey, module.AccentResourceSource),
            _ => ("AccentTextFillColorPrimaryBrush", null)
        };

        if (resourceSource is FrameworkElement source &&
            source.Resources.TryGetValue(resourceKey, out object resource) &&
            resource is Brush sourceBrush)
        {
            return sourceBrush;
        }

        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out object fallbackResource) &&
            fallbackResource is Brush fallbackBrush)
        {
            return fallbackBrush;
        }

        return new SolidColorBrush(Color.FromArgb(255, 96, 205, 255));
    }

    public object ConvertBack(object value,
        Type targetType,
        object parameter,
        string language) => throw new NotSupportedException();
}
