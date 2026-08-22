using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glance.Application.Abstractions;
using System.Globalization;

namespace Glance.Hydration;

public sealed partial class HydrationViewModel :
    ObservableObject
{
    private readonly ITextLocalizer localizer;
    private readonly HydrationReminderPolicy reminderPolicy;
    private Action<double>? logDrink;
    private Action? undoLastDrink;
    private string trackingDate;
    private double lastServingMillilitres;
    private DateTimeOffset lastServingAt;
    private DateTimeOffset lastReminderAt;

    public HydrationViewModel(HydrationSettings settings, HydrationReminderPolicy reminderPolicy, ITextLocalizer localizer, DateTimeOffset now)
    {
        this.reminderPolicy = reminderPolicy;
        this.localizer = localizer;
        DailyGoalMillilitres = HydrationSettings.NormalizeDailyGoal(settings.DailyGoalMillilitres);
        ServingSizeMillilitres = HydrationSettings.NormalizeServingSize(settings.ServingSizeMillilitres);
        trackingDate = settings.TrackingDate;
        ConsumedMillilitres = Math.Max(0, settings.ConsumedMillilitres);
        lastServingMillilitres = Math.Max(0, settings.LastServingMillilitres);
        lastServingAt = settings.LastServingAt;
        lastReminderAt = settings.LastReminderAt;
        EnsureCurrentDay(now);
        UpdateDisplay(settings, now);
    }

    [ObservableProperty]
    private bool canUndo;

    [ObservableProperty]
    private double consumedMillilitres;

    [ObservableProperty]
    private double dailyGoalMillilitres;

    [ObservableProperty]
    private string detail = string.Empty;

    [ObservableProperty]
    private HydrationLevel level;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private double servingSizeMillilitres;

    [ObservableProperty]
    private string summary = string.Empty;

    public string CompactText => FormatProgress();

    public string LogDrinkLabel => localizer.GetText("LogDrinkAmount", FormatVolume(ServingSizeMillilitres));

    public string Title => localizer.GetText("ModuleTitle");

    public event EventHandler? StateChanged;

    public void ConfigureActions(Action<double> log, Action undo)
    {
        logDrink = log;
        undoLastDrink = undo;
    }

    public void Add(double millilitres, HydrationSettings settings, DateTimeOffset now)
    {
        EnsureCurrentDay(now);
        double amount = Math.Clamp(millilitres, 1, 6000);
        ConsumedMillilitres += amount;
        lastServingMillilitres = amount;
        lastServingAt = now;
        CanUndo = true;
        UpdateDisplay(settings, now);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo(HydrationSettings settings, DateTimeOffset now)
    {
        EnsureCurrentDay(now);

        if (!CanUndo)
        {
            return;
        }

        ConsumedMillilitres = Math.Max(0, ConsumedMillilitres - lastServingMillilitres);
        lastServingMillilitres = 0;
        lastServingAt = default;
        CanUndo = false;
        UpdateDisplay(settings, now);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetDay(HydrationSettings settings, DateTimeOffset now)
    {
        EnsureCurrentDay(now);
        ConsumedMillilitres = 0;
        lastServingMillilitres = 0;
        lastServingAt = default;
        CanUndo = false;
        UpdateDisplay(settings, now);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Refresh(HydrationSettings settings, DateTimeOffset now)
    {
        bool reset = EnsureCurrentDay(now);
        UpdateDisplay(settings, now);

        if (reset)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        return reset;
    }

    public void ApplySettings(HydrationSettings settings, DateTimeOffset now)
    {
        DailyGoalMillilitres = HydrationSettings.NormalizeDailyGoal(settings.DailyGoalMillilitres);
        ServingSizeMillilitres = HydrationSettings.NormalizeServingSize(settings.ServingSizeMillilitres);
        OnPropertyChanged(nameof(LogDrinkLabel));
        UpdateDisplay(settings, now);
    }

    public HydrationSnapshot CreateSnapshot() => new(ConsumedMillilitres, DailyGoalMillilitres, lastReminderAt);

    public void RecordReminder(DateTimeOffset now)
    {
        lastReminderAt = now;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void WriteState(HydrationSettings settings)
    {
        settings.TrackingDate = trackingDate;
        settings.ConsumedMillilitres = ConsumedMillilitres;
        settings.LastServingMillilitres = lastServingMillilitres;
        settings.LastServingAt = lastServingAt;
        settings.LastReminderAt = lastReminderAt;
    }

    [RelayCommand]
    private void LogConfiguredDrink() => logDrink?.Invoke(ServingSizeMillilitres);

    [RelayCommand]
    private void UndoLastDrink() => undoLastDrink?.Invoke();

    private bool EnsureCurrentDay(DateTimeOffset now)
    {
        string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (string.Equals(trackingDate, today, StringComparison.Ordinal))
        {
            CanUndo = lastServingMillilitres > 0 && lastServingAt.LocalDateTime.Date == now.LocalDateTime.Date;
            return false;
        }

        trackingDate = today;
        ConsumedMillilitres = 0;
        lastServingMillilitres = 0;
        lastServingAt = default;
        lastReminderAt = default;
        CanUndo = false;
        return true;
    }

    private void UpdateDisplay(HydrationSettings settings, DateTimeOffset now)
    {
        Progress = Math.Clamp(ConsumedMillilitres / DailyGoalMillilitres, 0, 1);
        Level = reminderPolicy.GetLevel(settings, CreateSnapshot(), now);
        Summary = FormatProgress();
        Detail = Level == HydrationLevel.GoalReached
            ? localizer.GetText("GoalReached")
            : localizer.GetText("RemainingAmount", FormatVolume(Math.Max(0, DailyGoalMillilitres - ConsumedMillilitres)));
        OnPropertyChanged(nameof(CompactText));
    }

    private string FormatProgress() => localizer.GetText("ProgressAmount", FormatVolume(ConsumedMillilitres), FormatVolume(DailyGoalMillilitres));

    private static string FormatVolume(double millilitres) => millilitres >= 1000
        ? string.Format(CultureInfo.CurrentCulture, "{0:0.##} L", millilitres / 1000)
        : string.Format(CultureInfo.CurrentCulture, "{0:0} ml", millilitres);
}
