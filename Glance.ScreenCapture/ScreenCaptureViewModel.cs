using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using System.Collections.ObjectModel;

namespace Glance.ScreenCapture;

public sealed partial class ScreenCaptureViewModel : ObservableObject
{
    private readonly ITextLocalizer localizer;
    private int recentCaptureLimit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private bool isCapturing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactStatusText))]
    private string statusText;

    [ObservableProperty]
    private bool hasCaptures;

    [ObservableProperty]
    private ScreenCaptureItemViewModel? selectedCapture;

    public ScreenCaptureViewModel(ITextLocalizer localizer, ScreenCaptureSettings? settings = null)
    {
        this.localizer = localizer;
        recentCaptureLimit = GetRecentCaptureLimit(settings ?? new ScreenCaptureSettings());
        statusText = localizer.GetText("ReadyToCapture");
    }

    public string Title => localizer.GetText("ModuleTitle");

    public string CaptureRegionText => localizer.GetText("CaptureRegion");

    public string CaptureWindowText => localizer.GetText("CaptureWindow");

    public string CaptureDisplayText => localizer.GetText("CaptureDisplay");

    public string CaptureAllDisplaysText => localizer.GetText("CaptureAllDisplays");

    public string CompactStatusText => IsCapturing
        ? localizer.GetText("SelectingCapture")
        : StatusText;

    public ObservableCollection<ScreenCaptureItemViewModel> Captures { get; } = [];

    public event EventHandler<ScreenCaptureMode>? CaptureRequested;

    public event EventHandler<ScreenCaptureItem>? OpenRequested;

    public event EventHandler<ScreenCaptureItem>? RevealRequested;

    public event EventHandler<ScreenCaptureItem>? CopyRequested;

    public event EventHandler<ScreenCaptureItem>? DeleteRequested;

    public void CaptureRegion() => RequestCapture(ScreenCaptureMode.Region);

    public void CaptureWindow() => RequestCapture(ScreenCaptureMode.Window);

    public void CaptureDisplay() => RequestCapture(ScreenCaptureMode.Display);

    public void CaptureAllDisplays() => RequestCapture(ScreenCaptureMode.AllDisplays);

    public void SetCaptures(IEnumerable<ScreenCaptureItem> captures)
    {
        Captures.Clear();

        foreach (ScreenCaptureItem capture in captures)
        {
            Captures.Add(CreateItem(capture));
        }

        TrimCaptures();

        UpdateSelection();
    }

    public void CompleteCapture(ScreenCaptureItem? capture)
    {
        IsCapturing = false;

        if (capture is null)
        {
            StatusText = localizer.GetText("ReadyToCapture");
            return;
        }

        ScreenCaptureItemViewModel? existing = Captures.FirstOrDefault(item => string.Equals(item.Capture.FilePath, capture.FilePath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            Captures.Remove(existing);
        }

        Captures.Insert(0, CreateItem(capture));

        TrimCaptures();

        HasCaptures = true;
        SelectedCapture = Captures[0];
        StatusText = localizer.GetText("CaptureSaved");
    }

    public void ShowCaptureError()
    {
        IsCapturing = false;
        StatusText = localizer.GetText("CaptureFailed");
    }

    public void Remove(ScreenCaptureItem capture)
    {
        ScreenCaptureItemViewModel? item = Captures.FirstOrDefault(value => string.Equals(value.Capture.FilePath, capture.FilePath, StringComparison.OrdinalIgnoreCase));

        if (item is not null)
        {
            Captures.Remove(item);
        }

        UpdateSelection();
    }

    public void ApplySettings(ScreenCaptureSettings settings)
    {
        recentCaptureLimit = GetRecentCaptureLimit(settings);
        TrimCaptures();
        UpdateSelection();
    }

    private void TrimCaptures()
    {
        while (Captures.Count > recentCaptureLimit)
        {
            Captures.RemoveAt(Captures.Count - 1);
        }
    }

    private static int GetRecentCaptureLimit(ScreenCaptureSettings settings) =>
        (int)Math.Clamp(settings.RecentCaptureLimit, 1, 12);

    private void RequestCapture(ScreenCaptureMode mode)
    {
        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;
        StatusText = localizer.GetText("SelectingCapture");
        CaptureRequested?.Invoke(this, mode);
    }

    private void UpdateSelection()
    {
        HasCaptures = Captures.Count > 0;
        SelectedCapture = Captures.FirstOrDefault();
        StatusText = HasCaptures
            ? localizer.GetText("CaptureSaved")
            : localizer.GetText("ReadyToCapture");
    }

    private ScreenCaptureItemViewModel CreateItem(ScreenCaptureItem capture) =>
        new(
            capture,
            localizer.GetText("CaptureDetail", capture.Width, capture.Height),
            value => OpenRequested?.Invoke(this, value),
            value => RevealRequested?.Invoke(this, value),
            value =>
            {
                CopyRequested?.Invoke(this, value);
                return Task.CompletedTask;
            },
            value => DeleteRequested?.Invoke(this, value));
}
