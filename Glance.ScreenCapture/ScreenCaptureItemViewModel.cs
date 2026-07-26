namespace Glance.ScreenCapture;

public sealed class ScreenCaptureItemViewModel(ScreenCaptureItem capture,
    string detail,
    Action<ScreenCaptureItem> open,
    Action<ScreenCaptureItem> reveal,
    Func<ScreenCaptureItem, Task> copy,
    Action<ScreenCaptureItem> delete)
{
    private readonly Func<ScreenCaptureItem, Task> copy = copy;
    private readonly Action<ScreenCaptureItem> delete = delete;
    private readonly Action<ScreenCaptureItem> open = open;
    private readonly Action<ScreenCaptureItem> reveal = reveal;

    public ScreenCaptureItem Capture { get; } = capture;

    public string FileName => Capture.FileName;

    public string Detail { get; } = detail;

    public void Open() => open(Capture);

    public void Reveal() => reveal(Capture);

    public async void Copy() => await copy(Capture);

    public void Delete() => delete(Capture);
}
