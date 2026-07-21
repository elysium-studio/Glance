namespace Glance.ScreenCapture;

public sealed class ScreenCaptureItemViewModel
{
    private readonly Func<ScreenCaptureItem, Task> copy;
    private readonly Action<ScreenCaptureItem> delete;
    private readonly Action<ScreenCaptureItem> open;
    private readonly Action<ScreenCaptureItem> reveal;

    public ScreenCaptureItemViewModel(
        ScreenCaptureItem capture,
        string detail,
        Action<ScreenCaptureItem> open,
        Action<ScreenCaptureItem> reveal,
        Func<ScreenCaptureItem, Task> copy,
        Action<ScreenCaptureItem> delete)
    {
        Capture = capture;
        Detail = detail;
        this.open = open;
        this.reveal = reveal;
        this.copy = copy;
        this.delete = delete;
    }

    public ScreenCaptureItem Capture { get; }

    public string FileName => Capture.FileName;

    public string Detail { get; }

    public void Open() => open(Capture);

    public void Reveal() => reveal(Capture);

    public async void Copy() => await copy(Capture);

    public void Delete() => delete(Capture);
}
