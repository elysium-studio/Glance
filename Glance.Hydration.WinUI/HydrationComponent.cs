using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Hydration.WinUI;

public sealed class HydrationComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceAttentionComponent,
    IGlanceBackgroundComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceFooterAppearanceComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ModuleResourceTextLocalizer<HydrationModule> localizer;
    private readonly GlanceModuleOptions<HydrationSettings> options;
    private readonly HydrationReminderPolicy reminderPolicy;
    private readonly TimeProvider timeProvider;
    private readonly DispatcherQueueTimer timer;
    private readonly HydrationViewModel viewModel;
    private readonly IWritableOptions<HydrationSettings> writer;

    public HydrationComponent(HydrationViewModel viewModel, HydrationReminderPolicy reminderPolicy, IGlanceAttentionService attentionService, GlanceModuleOptions<HydrationSettings> options, IWritableOptions<HydrationSettings> writer, TimeProvider timeProvider, ModuleResourceTextLocalizer<HydrationModule> localizer)
    {
        this.viewModel = viewModel;
        this.reminderPolicy = reminderPolicy;
        this.attentionService = attentionService;
        this.options = options;
        this.writer = writer;
        this.timeProvider = timeProvider;
        this.localizer = localizer;
        HydrationCompactView compactView = new(viewModel);
        HydrationExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        BackgroundContent = new HydrationScene { ViewModel = viewModel };
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        dispatcherQueue = compactView.DispatcherQueue;
        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.IsRepeating = true;
        timer.Tick += HandleTimerTick;
        viewModel.ConfigureActions(LogDrink, UndoLastDrink);
        viewModel.StateChanged += HandleStateChanged;
        options.Changed += HandleOptionsChanged;
        timer.Start();
        _ = PersistStateAsync();
    }

    public string Id => "Hydration";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Health;

    public int Order => 14;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object BackgroundContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public uint? FooterForegroundColor => 0xFFF8FAFC;

    public bool IsAttentionEnabledByDefault => true;

    public event EventHandler? FooterAppearanceChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        new GlanceActionDescriptor("Hydration.LogDrink", Id, "Log a drink", "Add a drink to today's hydration total. Use the configured serving size when no amount is supplied.", [new GlanceActionParameterDescriptor("millilitres", GlanceActionParameterType.Number, "Drink volume in millilitres.", IsRequired: false, Minimum: 1, Maximum: 6000)])
        {
            SemanticTags = ["water", "drink", "hydration", "fluid", "millilitres", "litres"],
            ExampleUtterances = ["log a glass of water", "add 250 millilitres", "I drank half a litre of water"]
        },
        new GlanceActionDescriptor("Hydration.Undo", Id, "Undo last drink", "Remove the most recently logged drink from today's hydration total.")
        {
            SemanticTags = ["water", "drink", "hydration", "undo", "remove"],
            ExampleUtterances = ["undo my last drink", "remove the water I just logged"]
        }
    ];

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "Hydration.LogDrink", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        double? millilitres = request.GetNumber("millilitres");
        return Task.FromResult<GlanceActionResult?>(millilitres is null or >= 1 and <= 6000
            ? null
            : GlanceActionResult.InvalidArguments("That drink amount isn't supported.", "Choose an amount between 1 and 6,000 millilitres."));
    }

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request, CancellationToken cancellationToken = default)
    {
        switch (request.ActionId)
        {
            case "Hydration.LogDrink":
                LogDrink(request.GetNumber("millilitres") ?? viewModel.ServingSizeMillilitres);
                return Task.FromResult(GlanceActionResult.Success());
            case "Hydration.Undo" when viewModel.CanUndo:
                UndoLastDrink();
                return Task.FromResult(GlanceActionResult.Success());
            default:
                return Task.FromResult(GlanceActionResult.Unavailable());
        }
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTimerTick;
        viewModel.StateChanged -= HandleStateChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private void LogDrink(double millilitres) => viewModel.Add(millilitres, options.Current, timeProvider.GetLocalNow());

    private void UndoLastDrink() => viewModel.Undo(options.Current, timeProvider.GetLocalNow());

    private void HandleTimerTick(DispatcherQueueTimer sender, object args)
    {
        DateTimeOffset now = timeProvider.GetLocalNow();
        _ = viewModel.Refresh(options.Current, now);

        if (!reminderPolicy.ShouldRemind(options.Current, viewModel.CreateSnapshot(), now))
        {
            return;
        }

        viewModel.RecordReminder(now);
        attentionService.RequestAttention(Id, viewModel.Level == HydrationLevel.Critical ? GlanceAttentionLevel.Critical : GlanceAttentionLevel.Default);
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<HydrationSettings> args) => _ = dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options, timeProvider.GetLocalNow()));

    private void HandleStateChanged(object? sender, EventArgs args) => _ = PersistStateAsync();

    private async Task PersistStateAsync()
    {
        try
        {
            await writer.WriteAsync(viewModel.WriteState);
        }
        catch
        { }
    }
}
