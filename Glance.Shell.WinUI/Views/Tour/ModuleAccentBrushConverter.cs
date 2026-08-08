using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleAccentBrushConverter :
    IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is SetupTourModuleViewModel module &&
            module.AccentResourceSource is FrameworkElement source &&
            source.Resources.TryGetValue(module.AccentResourceKey, out object resource) &&
            resource is Brush sourceBrush)
        {
            return sourceBrush;
        }

        if (value is SetupTourModuleViewModel fallbackModule &&
            Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(fallbackModule.AccentResourceKey, out object fallbackResource) &&
            fallbackResource is Brush fallbackBrush)
        {
            return fallbackBrush;
        }

        return Microsoft.UI.Xaml.Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
