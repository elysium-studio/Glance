using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Glance.ColorPicker;

public partial class ColorPickerViewModel :
    ObservableObject,
    IDisposable
{
    private const int MaximumRecentColors = 6;
    private readonly IColorPickerService colorPickerService;
    private readonly ITextCopyService textCopyService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hex))]
    [NotifyPropertyChangedFor(nameof(Rgb))]
    [NotifyPropertyChangedFor(nameof(Hsl))]
    private ColorValue currentColor = new(0, 120, 212);

    [ObservableProperty]
    private bool isPicking;

    public ColorPickerViewModel(
        IColorPickerService colorPickerService,
        ITextCopyService textCopyService)
    {
        this.colorPickerService = colorPickerService;
        this.textCopyService = textCopyService;

        colorPickerService.PreviewChanged += HandlePreviewChanged;
        colorPickerService.ColorPicked += HandleColorPicked;
        colorPickerService.PickingCancelled += HandlePickingCancelled;
    }

    public string Hex => CurrentColor.Hex;

    public string Rgb => CurrentColor.Rgb;

    public string Hsl => CurrentColor.Hsl;

    public ObservableCollection<ColorValue> RecentColors { get; } = [];

    public void Pick()
    {
        if (IsPicking)
        {
            colorPickerService.CancelPicking();
            return;
        }

        IsPicking = true;
        colorPickerService.StartPicking();
    }

    public void CopyHex() => _ = textCopyService.CopyAsync(Hex);

    public void CopyRgb() => _ = textCopyService.CopyAsync(Rgb);

    public void CopyHsl() => _ = textCopyService.CopyAsync(Hsl);

    public void SelectRecent(ColorValue color) => CurrentColor = color;

    public void Dispose()
    {
        colorPickerService.PreviewChanged -= HandlePreviewChanged;
        colorPickerService.ColorPicked -= HandleColorPicked;
        colorPickerService.PickingCancelled -= HandlePickingCancelled;
    }

    private void HandlePreviewChanged(object? sender, ColorPickerEventArgs args) =>
        CurrentColor = args.Color;

    private void HandleColorPicked(object? sender, ColorPickerEventArgs args)
    {
        CurrentColor = args.Color;
        IsPicking = false;

        for (int index = RecentColors.Count - 1; index >= 0; index--)
        {
            if (RecentColors[index] == args.Color)
            {
                RecentColors.RemoveAt(index);
            }
        }

        RecentColors.Insert(0, args.Color);

        while (RecentColors.Count > MaximumRecentColors)
        {
            RecentColors.RemoveAt(RecentColors.Count - 1);
        }
    }

    private void HandlePickingCancelled(object? sender, EventArgs args) =>
        IsPicking = false;
}
