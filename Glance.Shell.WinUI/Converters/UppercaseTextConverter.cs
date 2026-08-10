using Microsoft.UI.Xaml.Data;
using System;

namespace Glance.Shell.WinUI;

public sealed partial class UppercaseTextConverter :
    IValueConverter
{
    public object Convert(object value,
        Type targetType,
        object parameter,
        string language) => value is string text ?
            text.ToUpperInvariant() :
            string.Empty;

    public object ConvertBack(object value,
        Type targetType,
        object parameter,
        string language) => throw new NotSupportedException();
}
