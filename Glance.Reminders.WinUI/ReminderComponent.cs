using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Globalization;

namespace Glance.Reminders.WinUI;

public sealed partial class ReminderComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceAttentionComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly ReminderAttentionTracker attentionTracker = new();
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ReminderExpandedView expandedView;
    private readonly ModuleResourceTextLocalizer<ReminderModule> localizer;
    private readonly ReminderRepository repository;
    private readonly TimeProvider timeProvider;
    private readonly DispatcherQueueTimer timer;
    private readonly ReminderViewModel viewModel;

    public ReminderComponent(ReminderViewModel viewModel,
        ReminderRepository repository,
        IGlanceAttentionService attentionService,
        TimeProvider timeProvider,
        ModuleResourceTextLocalizer<ReminderModule> localizer)
    {
        this.viewModel = viewModel;
        this.repository = repository;
        this.attentionService = attentionService;
        this.timeProvider = timeProvider;
        this.localizer = localizer;
        ReminderCompactView compactView = new(viewModel);
        expandedView = new ReminderExpandedView(viewModel, localizer);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        dispatcherQueue = compactView.DispatcherQueue;
        viewModel.ConfigureActions(EditAsync, RemoveAsync);
        IReadOnlyList<ReminderEntry> reminders = repository.Load();
        viewModel.Restore(reminders);
        attentionTracker.Initialize(reminders, timeProvider.GetLocalNow());
        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.IsRepeating = true;
        timer.Tick += HandleTimerTick;
        timer.Start();
    }

    public string Id => "Reminders";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 12;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("Reminders.Add",
            Id,
            "Add a reminder",
            "Create a reminder. Open the reminder editor when the request does not include both its subject and due time.",
            [
                new GlanceActionParameterDescriptor("title", GlanceActionParameterType.String, "What the user wants to be reminded about.", IsRequired: false),
                new GlanceActionParameterDescriptor("when", GlanceActionParameterType.String, "The local due date and time in ISO 8601 format. Resolve relative phrases such as today or tomorrow using the current local date and time.", IsRequired: false),
                new GlanceActionParameterDescriptor("priority", GlanceActionParameterType.String, "The reminder priority.", IsRequired: false, AllowedValues: ["low", "normal", "high"])
            ],
            Presentation: GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["reminder", "remind", "remember", "appointment", "due", "alert", "priority", "schedule"],
            ExampleUtterances = ["add a reminder", "remind me at 4 PM today about my dentist appointment", "set a high priority reminder for tomorrow morning"]
        }
    ];

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "Reminders.Add", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        return Task.FromResult<GlanceActionResult?>(null);
    }

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? title = request.GetString("title")?.Trim();
        string? when = request.GetString("when");
        ReminderPriority priority = ParsePriority(request.GetString("priority"));
        DateTimeOffset? parsedDueAt = TryParseDueAt(when, out DateTimeOffset dueAt) ? dueAt : null;
        DateTimeOffset now = timeProvider.GetLocalNow();
        DateTimeOffset? validDueAt = parsedDueAt > now ? parsedDueAt : null;

        if (!string.IsNullOrWhiteSpace(title) && validDueAt is DateTimeOffset completeDueAt)
        {
            ReminderEntry entry = CreateEntry(new ReminderDraft(title, completeDueAt, priority));
            await RunOnDispatcherAsync(() =>
            {
                Save(entry);
                return true;
            });
            return GlanceActionResult.Success($"Reminder set for {entry.DueAt.LocalDateTime:g}.");
        }

        ReminderDraft? draft = null;

        if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(when))
        {
            draft = new ReminderDraft(title ?? string.Empty, validDueAt ?? now.AddHours(1), priority);
        }

        ReminderEntry? created = await OpenEditorAsync(null, draft);
        return created is null
            ? GlanceActionResult.Success("No reminder added.")
            : GlanceActionResult.Success($"Reminder set for {created.DueAt.LocalDateTime:g}.");
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTimerTick;
    }

    private async Task EditAsync(ReminderItemViewModel? item) =>
        await OpenEditorAsync(item, item is null ? null : new ReminderDraft(item.Title, item.DueAt, item.Priority));

    private async Task<ReminderEntry?> OpenEditorAsync(ReminderItemViewModel? item,
        ReminderDraft? draft)
    {
        WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

        if (ownerWindowId is not WindowId windowId)
        {
            return null;
        }

        Task<ReminderDraft?> editorTask = await RunOnDispatcherAsync(() => ReminderEditorWindow.ShowAsync(draft, localizer, windowId));
        ReminderDraft? result = await editorTask;

        if (result is null)
        {
            return null;
        }

        return await RunOnDispatcherAsync(() =>
        {
            ReminderEntry entry = item is null
                ? CreateEntry(result)
                : new ReminderEntry(item.Id, result.Title, result.DueAt, result.Priority, item.CreatedAt);
            Save(entry);
            return entry;
        });
    }

    private Task<T> RunOnDispatcherAsync<T>(Func<T> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(action());
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("The Reminders UI dispatcher is unavailable."));
        }

        return completion.Task;
    }

    private Task RemoveAsync(ReminderItemViewModel item)
    {
        repository.Remove(item.Id);
        attentionTracker.Remove(item.Id);
        viewModel.Remove(item);
        return Task.CompletedTask;
    }

    private void Save(ReminderEntry entry)
    {
        repository.Save(entry);
        viewModel.Upsert(entry);
        attentionTracker.Track(entry, timeProvider.GetLocalNow());
    }

    private ReminderEntry CreateEntry(ReminderDraft draft) =>
        new(Guid.NewGuid().ToString("N"), draft.Title.Trim(), draft.DueAt, draft.Priority, timeProvider.GetUtcNow());

    private void HandleTimerTick(DispatcherQueueTimer sender,
        object args)
    {
        IReadOnlyList<ReminderAttentionChange> changes = attentionTracker.Update(viewModel.Reminders.Select(item => item.ToEntry()), timeProvider.GetLocalNow());

        foreach (ReminderAttentionChange change in changes.OrderByDescending(change => change.State).ThenByDescending(change => change.Reminder.Priority))
        {
            viewModel.SelectedReminder = viewModel.Reminders.FirstOrDefault(item => item.Id == change.Reminder.Id);
            attentionService.RequestAttention(Id, change.State == ReminderAttentionState.Due ? GlanceAttentionLevel.Critical : GlanceAttentionLevel.Default);
            break;
        }
    }

    private static ReminderPriority ParsePriority(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "low" => ReminderPriority.Low,
        "high" => ReminderPriority.High,
        _ => ReminderPriority.Normal
    };

    private static bool TryParseDueAt(string? value,
        out DateTimeOffset dueAt)
    {
        dueAt = default;
        return !string.IsNullOrWhiteSpace(value) &&
            (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out dueAt) ||
             DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out dueAt));
    }
}
