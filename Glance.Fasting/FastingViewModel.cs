using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.Fasting;

public sealed partial class FastingViewModel :
    ObservableObject
{
    private readonly ITextLocalizer localizer;
    private Action? reset;
    private Action? toggle;
    private DateTimeOffset startedAt;
    private DateTimeOffset endsAt;
    private TimeSpan preferredDuration;

    public FastingViewModel(FastingSettings settings, ITextLocalizer localizer, DateTimeOffset now)
    {
        this.localizer = localizer;
        preferredDuration = FastingPlanCatalog.GetFastingDuration(settings.Plan, settings.CustomFastingHours);
        startedAt = settings.StartedAt;
        endsAt = settings.EndsAt;
        Status = settings.SessionStatus;
        CompletionAttentionSent = settings.CompletionAttentionSent;

        if (Status == FastingSessionStatus.Fasting && (startedAt == default || endsAt <= startedAt))
        {
            Status = FastingSessionStatus.Ready;
        }

        UpdateDisplay(now);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFasting))]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    private FastingSessionStatus status;

    [ObservableProperty]
    private bool completionAttentionSent;

    [ObservableProperty]
    private string detail = string.Empty;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private TimeSpan remaining;

    [ObservableProperty]
    private FastingStage stage;

    [ObservableProperty]
    private string summary = string.Empty;

    public string CompactText => Status switch
    {
        FastingSessionStatus.Fasting => localizer.GetText("CompactRemaining", FormatRemaining(Remaining)),
        FastingSessionStatus.Completed => localizer.GetText("FastComplete"),
        _ => localizer.GetText("ReadyToFast")
    };

    public bool IsFasting => Status == FastingSessionStatus.Fasting;

    public string Title => localizer.GetText("ModuleTitle");

    public string ToggleGlyph => IsFasting ? "\uF8AE" : "\uF5B0";

    public event EventHandler? StateChanged;

    public void ConfigureActions(Action toggleAction, Action resetAction)
    {
        toggle = toggleAction;
        reset = resetAction;
    }

    public void Start(FastingSettings settings, DateTimeOffset now)
    {
        preferredDuration = FastingPlanCatalog.GetFastingDuration(settings.Plan, settings.CustomFastingHours);
        startedAt = now;
        endsAt = now + preferredDuration;
        Status = FastingSessionStatus.Fasting;
        CompletionAttentionSent = false;
        UpdateDisplay(now);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop(DateTimeOffset now)
    {
        Status = FastingSessionStatus.Ready;
        startedAt = default;
        endsAt = default;
        CompletionAttentionSent = false;
        UpdateDisplay(now);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset(DateTimeOffset now) => Stop(now);

    public bool Refresh(DateTimeOffset now)
    {
        FastingSessionStatus previousStatus = Status;
        UpdateDisplay(now);

        if (previousStatus == Status)
        {
            return false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return Status == FastingSessionStatus.Completed;
    }

    public void ApplySettings(FastingSettings settings, DateTimeOffset now)
    {
        preferredDuration = FastingPlanCatalog.GetFastingDuration(settings.Plan, settings.CustomFastingHours);
        UpdateDisplay(now);
    }

    public void MarkCompletionAttentionSent()
    {
        if (CompletionAttentionSent)
        {
            return;
        }

        CompletionAttentionSent = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void WriteState(FastingSettings settings)
    {
        settings.SessionStatus = Status;
        settings.StartedAt = startedAt;
        settings.EndsAt = endsAt;
        settings.CompletionAttentionSent = CompletionAttentionSent;
    }

    [RelayCommand]
    private void ResetSession() => reset?.Invoke();

    [RelayCommand]
    private void ToggleSession() => toggle?.Invoke();

    private void UpdateDisplay(DateTimeOffset now)
    {
        if (Status == FastingSessionStatus.Fasting && now >= endsAt)
        {
            Status = FastingSessionStatus.Completed;
        }

        TimeSpan sessionDuration = startedAt != default && endsAt > startedAt ? endsAt - startedAt : preferredDuration;

        if (Status == FastingSessionStatus.Ready)
        {
            Remaining = preferredDuration;
            Progress = 0;
            Stage = FastingStage.Ready;
            Summary = localizer.GetText("ReadyToFast");
            Detail = localizer.GetText("ReadyDetail", FormatDuration(preferredDuration));
        }
        else if (Status == FastingSessionStatus.Completed)
        {
            Remaining = TimeSpan.Zero;
            Progress = 1;
            Stage = FastingStage.Completed;
            Summary = localizer.GetText("FastComplete");
            Detail = localizer.GetText("CompletedDetail", FormatDuration(sessionDuration));
        }
        else
        {
            Remaining = endsAt - now;
            Progress = Math.Clamp((now - startedAt).TotalMilliseconds / sessionDuration.TotalMilliseconds, 0, 1);
            Stage = GetStage(Progress);
            Summary = FormatRemaining(Remaining);
            Detail = localizer.GetText(GetStageTextKey(Stage), FormatDuration(sessionDuration));
        }

        OnPropertyChanged(nameof(CompactText));
    }

    private static FastingStage GetStage(double progress) => progress switch
    {
        < 0.15 => FastingStage.GettingStarted,
        < 0.5 => FastingStage.SettledIn,
        < 0.8 => FastingStage.Halfway,
        _ => FastingStage.FinalStretch
    };

    private static string GetStageTextKey(FastingStage stage) => stage switch
    {
        FastingStage.GettingStarted => "StageGettingStarted",
        FastingStage.SettledIn => "StageSettledIn",
        FastingStage.Halfway => "StageHalfway",
        _ => "StageFinalStretch"
    };

    private static string FormatDuration(TimeSpan duration) => string.Format(CultureInfo.CurrentCulture, "{0:0.#} h", duration.TotalHours);

    private static string FormatRemaining(TimeSpan remaining)
    {
        TimeSpan value = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }
}
