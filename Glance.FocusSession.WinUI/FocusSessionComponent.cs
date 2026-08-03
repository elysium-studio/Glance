using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusSessionComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly IGlanceAttentionService attentionService;
    private readonly ITextLocalizer localizer;
    private readonly DispatcherQueueTimer timer;
    private readonly FocusSessionViewModel viewModel;
    private readonly GlanceModuleOptions<FocusSessionSettings> options;
    private readonly IWritableOptions<FocusSessionSettings> writer;

    public FocusSessionComponent(FocusSessionViewModel viewModel,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<FocusSessionSettings> options,
        IWritableOptions<FocusSessionSettings> writer,
        ModuleResourceTextLocalizer<FocusSessionModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.options = options;
        this.writer = writer;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        FocusSessionCompactView compactView = new(viewModel);
        FocusSessionExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;

        viewModel.PropertyChanged += HandlePropertyChanged;
        viewModel.SessionStateChanged += HandleSessionStateChanged;
        options.Changed += HandleOptionsChanged;

        if (viewModel.IsRunning)
        {
            timer.Start();
        }

        _ = PersistSessionAsync();
    }

    public string Id => "FocusSession";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 70;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("FocusSession.Start", Id, "Start focus session", "Start or resume the Pomodoro-style focus session.")
        {
            SemanticTags = ["focus", "focus session", "pomodoro", "productivity", "concentrate", "resume"],
            ExampleUtterances = ["start a focus session", "begin focusing", "resume my Pomodoro"]
        },
        new GlanceActionDescriptor("FocusSession.Pause", Id, "Pause focus session", "Pause the current focus or break interval.")
        {
            SemanticTags = ["focus", "focus session", "pomodoro", "pause", "hold"],
            ExampleUtterances = ["pause my focus session", "hold the Pomodoro"]
        },
        new GlanceActionDescriptor("FocusSession.Skip", Id, "Skip focus interval", "Skip to the next focus or break interval.")
        {
            SemanticTags = ["focus", "focus session", "pomodoro", "skip", "next interval", "break"],
            ExampleUtterances = ["skip this focus interval", "go to my break", "move to the next Pomodoro interval"]
        },
        new GlanceActionDescriptor("FocusSession.Reset", Id, "Reset focus session", "Reset the complete focus session.")
        {
            SemanticTags = ["focus", "focus session", "pomodoro", "reset", "restart"],
            ExampleUtterances = ["reset my focus session", "restart the Pomodoro"]
        }
    ];

    public bool IsAvailable(string actionId) =>
        actionId switch
        {
            "FocusSession.Start" => !viewModel.IsRunning,
            "FocusSession.Pause" => viewModel.IsRunning,
            _ => true
        };

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (request.ActionId)
        {
            case "FocusSession.Start" or "FocusSession.Pause":
                viewModel.Toggle();
                break;
            case "FocusSession.Skip":
                viewModel.Skip();
                break;
            case "FocusSession.Reset":
                viewModel.Reset();
                break;
            default:
                return Task.FromResult(GlanceActionResult.Unavailable());
        }

        return Task.FromResult(GlanceActionResult.Success());
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.PropertyChanged -= HandlePropertyChanged;
        viewModel.SessionStateChanged -= HandleSessionStateChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<FocusSessionSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FocusSessionViewModel.IsRunning))
        {
            return;
        }

        if (viewModel.IsRunning)
        {
            timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private void HandleTick(DispatcherQueueTimer sender, object args)
    {
        if (viewModel.Refresh() is not null)
        {
            attentionService.RequestAttention(Id);
        }
    }

    private async void HandleSessionStateChanged(object? sender, EventArgs args) =>
        await PersistSessionAsync();

    private async Task PersistSessionAsync()
    {
        try
        {
            await writer.WriteAsync(settings => viewModel.WriteSessionState(settings));
        }
        catch
        { }
    }
}
