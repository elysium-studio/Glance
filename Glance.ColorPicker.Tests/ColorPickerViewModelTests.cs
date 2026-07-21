namespace Glance.ColorPicker.Tests;

public sealed class ColorPickerViewModelTests
{
    [Fact]
    public void Pick_StartsAndCancelsPicker()
    {
        FakeColorPickerService picker = new();
        ColorPickerViewModel viewModel = new(picker, new FakeTextCopyService());

        viewModel.Pick();

        Assert.True(viewModel.IsPicking);
        Assert.Equal(1, picker.StartCount);

        viewModel.Pick();

        Assert.False(viewModel.IsPicking);
        Assert.Equal(1, picker.CancelCount);
    }

    [Fact]
    public void Preview_UpdatesCurrentColorWithoutAddingHistory()
    {
        FakeColorPickerService picker = new();
        ColorPickerViewModel viewModel = new(picker, new FakeTextCopyService());

        picker.Preview(new ColorValue(12, 34, 56));

        Assert.Equal("#0C2238", viewModel.Hex);
        Assert.Empty(viewModel.RecentColors);
    }

    [Fact]
    public void PickedColor_IsAddedToBoundedDeduplicatedHistory()
    {
        FakeColorPickerService picker = new();
        ColorPickerViewModel viewModel = new(picker, new FakeTextCopyService());

        for (byte value = 0; value < 8; value++)
        {
            picker.Complete(new ColorValue(value, value, value));
        }

        picker.Complete(new ColorValue(5, 5, 5));

        Assert.Equal(6, viewModel.RecentColors.Count);
        Assert.Equal(new ColorValue(5, 5, 5), viewModel.RecentColors[0]);
        Assert.Single(viewModel.RecentColors, color => color == new ColorValue(5, 5, 5));
    }

    [Fact]
    public void CopyFunctions_CopyExpectedFormats()
    {
        FakeTextCopyService clipboard = new();
        ColorPickerViewModel viewModel = new(new FakeColorPickerService(), clipboard);
        viewModel.SelectRecent(new ColorValue(2, 14, 30));

        viewModel.CopyHex();
        viewModel.CopyRgb();
        viewModel.CopyHsl();

        Assert.Equal(
            ["#020E1E", "rgb(2, 14, 30)", "hsl(214, 88%, 6%)"],
            clipboard.Values);
    }

    private sealed class FakeColorPickerService :
        IColorPickerService
    {
        public event EventHandler<ColorPickerEventArgs>? PreviewChanged;

        public event EventHandler<ColorPickerEventArgs>? ColorPicked;

        public event EventHandler? PickingCancelled;

        public bool IsPicking { get; private set; }

        public int StartCount { get; private set; }

        public int CancelCount { get; private set; }

        public void StartPicking()
        {
            StartCount++;
            IsPicking = true;
        }

        public void CancelPicking()
        {
            CancelCount++;
            IsPicking = false;
            PickingCancelled?.Invoke(this, EventArgs.Empty);
        }

        public void Preview(ColorValue color) =>
            PreviewChanged?.Invoke(this, new ColorPickerEventArgs(color));

        public void Complete(ColorValue color)
        {
            IsPicking = false;
            ColorPicked?.Invoke(this, new ColorPickerEventArgs(color));
        }
    }

    private sealed class FakeTextCopyService :
        ITextCopyService
    {
        public List<string> Values { get; } = [];

        public void Copy(string text) => Values.Add(text);
    }
}
