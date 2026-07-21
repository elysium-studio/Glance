namespace Glance.ColorPicker;

public interface IColorPickerService
{
    event EventHandler<ColorPickerEventArgs>? PreviewChanged;

    event EventHandler<ColorPickerEventArgs>? ColorPicked;

    event EventHandler? PickingCancelled;

    bool IsPicking { get; }

    void StartPicking();

    void CancelPicking();
}
