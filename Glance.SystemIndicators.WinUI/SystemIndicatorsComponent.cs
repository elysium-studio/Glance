using Glance.Application.Abstractions;
using Glance.SystemIndicators;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;

namespace Glance.SystemIndicators.WinUI;

public sealed class SystemIndicatorsComponent :
    IGlanceTransientComponent,
    IDisposable
{
    private static readonly TimeSpan PresentationDuration = TimeSpan.FromMilliseconds(1800);
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ISystemIndicatorService indicatorService;
    private readonly ITextLocalizer localizer;
    private readonly SystemIndicatorsViewModel viewModel;
    private readonly DispatcherQueueTimer dismissalTimer;
    private bool isDisposed;

    public SystemIndicatorsComponent(SystemIndicatorsViewModel viewModel,
        ISystemIndicatorService indicatorService,
        ModuleResourceTextLocalizer<SystemIndicatorsModule> localizer)
    {
        this.viewModel = viewModel;
        this.indicatorService = indicatorService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        dismissalTimer = dispatcherQueue.CreateTimer();
        dismissalTimer.Interval = PresentationDuration;
        dismissalTimer.IsRepeating = false;
        dismissalTimer.Tick += HandleDismissalTimerTick;

        CompactContent = new SystemIndicatorsCompactView(viewModel);
        ExpandedContent = new SystemIndicatorsExpandedView(viewModel);
        indicatorService.StateChanged += HandleStateChanged;
    }

    public event EventHandler<GlanceTransientPresentationRequestedEventArgs>? PresentationRequested;

    public event EventHandler? DismissalRequested;

    public string Id => "SystemIndicators";

    public bool IsPresentationEnabled
    {
        get => indicatorService.IsEnabled;
        set => indicatorService.IsEnabled = value;
    }

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.DevicesAndSystem;

    public int Order => 35;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        indicatorService.IsEnabled = false;
        indicatorService.StateChanged -= HandleStateChanged;
        dismissalTimer.Stop();
        dismissalTimer.Tick -= HandleDismissalTimerTick;
    }

    private void HandleStateChanged(object? sender,
        SystemIndicatorState state) => _ = dispatcherQueue.TryEnqueue(() => Present(state));

    private void Present(SystemIndicatorState state)
    {
        if (isDisposed)
        {
            return;
        }

        viewModel.Update(CreatePresentation(state));
        PresentationRequested?.Invoke(this, new GlanceTransientPresentationRequestedEventArgs());

        dismissalTimer.Stop();
        dismissalTimer.Start();
    }

    private void HandleDismissalTimerTick(DispatcherQueueTimer sender,
        object args)
    {
        if (!isDisposed)
        {
            DismissalRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private SystemIndicatorPresentation CreatePresentation(SystemIndicatorState state)
    {
        int? level = state.NormalizedLevel;

        return state.Kind switch
        {
            SystemIndicatorKind.Volume when state.IsEnabled == false => new(
                localizer.GetText("VolumeTitle"),
                localizer.GetText("MutedText"),
                localizer.GetText("VolumeMutedDetail"),
                "\uE74F",
                level),
            SystemIndicatorKind.Volume => new(
                localizer.GetText("VolumeTitle"),
                localizer.GetText("PercentText", level ?? 0),
                localizer.GetText("VolumeDetail"),
                "\uE767",
                level),
            SystemIndicatorKind.Brightness => new(
                localizer.GetText("BrightnessTitle"),
                localizer.GetText("PercentText", level ?? 0),
                localizer.GetText("BrightnessDetail"),
                "\uE706",
                level),
            SystemIndicatorKind.CapsLock => CreateLockPresentation("CapsLockTitle",
                "CapsLockOnDetail",
                "CapsLockOffDetail",
                state.IsEnabled == true),
            SystemIndicatorKind.NumLock => CreateLockPresentation("NumLockTitle",
                "NumLockOnDetail",
                "NumLockOffDetail",
                state.IsEnabled == true),
            SystemIndicatorKind.ScrollLock => CreateLockPresentation("ScrollLockTitle",
                "ScrollLockOnDetail",
                "ScrollLockOffDetail",
                state.IsEnabled == true),
            SystemIndicatorKind.AirplaneMode => CreateTogglePresentation("AirplaneModeTitle",
                "AirplaneModeOnDetail",
                "AirplaneModeOffDetail",
                "\uE709",
                state.IsEnabled == true),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }

    private SystemIndicatorPresentation CreateTogglePresentation(string titleKey,
        string onDetailKey,
        string offDetailKey,
        string glyph,
        bool isEnabled) => new(
            localizer.GetText(titleKey),
            localizer.GetText(isEnabled ? "OnText" : "OffText"),
            localizer.GetText(isEnabled ? onDetailKey : offDetailKey),
            glyph);

    private SystemIndicatorPresentation CreateLockPresentation(string titleKey,
        string onDetailKey,
        string offDetailKey,
        bool isEnabled) => CreateTogglePresentation(titleKey,
            onDetailKey,
            offDetailKey,
            isEnabled ? "\uE72E" : "\uE785",
            isEnabled);
}
