using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Timer.WinUI;

public sealed partial class TimerComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer timer;
    private readonly ITextLocalizer localizer;
    private readonly TimerViewModel viewModel;
    private readonly IGlanceAttentionService attentionService;
    private readonly GlanceModuleOptions<TimerSettings> options;
    private readonly IWritableOptions<TimerSettings> writer;

    public TimerComponent(TimerViewModel viewModel,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<TimerSettings> options,
        IWritableOptions<TimerSettings> writer,
        ModuleResourceTextLocalizer<TimerModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.options = options;
        this.writer = writer;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        TimerCompactView compactView = new(viewModel);
        TimerExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
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

    public string Id => "Timer";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 10;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => true;

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("Timer.Start",
            Id,
            "Start timer",
            "Start a countdown for a duration expressed as minutes. Convert hours or seconds into minutes.",
            [new GlanceActionParameterDescriptor("minutes", GlanceActionParameterType.Number, "Countdown duration in minutes. Convert spoken hours or seconds to minutes.", Minimum: 1d / 60, Maximum: 1440)])
        {
            SemanticTags = ["timer", "countdown", "alarm", "remind", "minutes", "seconds", "hours"],
            ExampleUtterances = ["set a timer for twenty four minutes", "start a ninety second countdown", "give me a timer for half an hour"]
        },
        new GlanceActionDescriptor("Timer.Pause", Id, "Pause timer", "Pause the running countdown.")
        {
            SemanticTags = ["timer", "countdown", "pause", "hold"],
            ExampleUtterances = ["pause the timer", "hold my countdown"]
        },
        new GlanceActionDescriptor("Timer.Resume", Id, "Resume timer", "Continue the paused countdown.")
        {
            SemanticTags = ["timer", "countdown", "resume", "continue"],
            ExampleUtterances = ["resume the timer", "continue my countdown"]
        },
        new GlanceActionDescriptor("Timer.Reset", Id, "Reset timer", "Reset the countdown to its configured duration.")
        {
            SemanticTags = ["timer", "countdown", "reset", "restart"],
            ExampleUtterances = ["reset the timer", "restart my countdown"]
        }
    ];

    public bool IsAvailable(string actionId) =>
        actionId switch
        {
            "Timer.Pause" => viewModel.IsRunning,
            "Timer.Resume" => !viewModel.IsRunning,
            _ => true
        };

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "Timer.Start", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        double? minutes = request.GetNumber("minutes");

        if (minutes is null)
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("How long should the timer run?", "Say a duration such as 24 minutes."));
        }

        return Task.FromResult<GlanceActionResult?>(minutes is >= 1d / 60 and <= 1440
            ? null
            : GlanceActionResult.InvalidArguments("That timer duration isn't supported.", "Choose between one second and 24 hours."));
    }

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (request.ActionId)
        {
            case "Timer.Start":
                viewModel.Start(TimeSpan.FromMinutes(request.GetNumber("minutes")!.Value));
                break;
            case "Timer.Pause":
                viewModel.Pause();
                break;
            case "Timer.Resume":
                viewModel.Resume();
                break;
            case "Timer.Reset":
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

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<TimerSettings> args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options));

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(TimerViewModel.IsRunning))
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
        if (viewModel.Refresh())
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
