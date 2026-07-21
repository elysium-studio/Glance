using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorSwatchItem
{
    private readonly Action<ColorValue> select;

    public ColorSwatchItem(
        ColorValue color,
        Action<ColorValue> select)
    {
        Color = color;
        this.select = select;
        Brush = new SolidColorBrush(ColorHelper.FromArgb(
            255,
            color.Red,
            color.Green,
            color.Blue));
    }

    public ColorValue Color { get; }

    public string Hex => Color.Hex;

    public SolidColorBrush Brush { get; }

    public void Select() => select(Color);
}
