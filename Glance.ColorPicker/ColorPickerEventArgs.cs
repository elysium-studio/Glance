namespace Glance.ColorPicker;

public sealed class ColorPickerEventArgs(ColorValue color) :
    EventArgs
{
    public ColorValue Color { get; } = color;
}
