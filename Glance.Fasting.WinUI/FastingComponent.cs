using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Fasting.WinUI;

public sealed class FastingComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IGlanceBackgroundComponent,
    IGlanceFooterAppearanceComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ModuleResourceTextLocalizer<FastingModule> localizer;
    private readonly GlanceModuleOptions<FastingSettings> options;
    private readonly TimeProvider timeProvider;
    private readonly DispatcherQueueTimer timer;
    private readonly FastingViewModel viewModel;
    private readonly IWritableOptions<FastingSettings> writer;

    public FastingComponent(FastingViewModel viewModel, IGlanceAttentionService attentionService, GlanceModuleOptions<FastingSettings> options, IWritableOptions<FastingSettings> writer, TimeProvider timeProvider, ModuleResourceTextLocalizer<FastingModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.options = options;
        this.writer = writer;
        this.timeProvider = timeProvider;
        this.localizer = localizer;
        FastingCompactView compactView = new(viewModel);
        FastingExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        BackgroundContent = new FastingScene { ViewModel = viewModel };
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        dispatcherQueue = compactView.DispatcherQueue;
        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += HandleTimerTick;
        viewModel.ConfigureActions(Toggle, Reset);
        viewModel.PropertyChanged += HandlePropertyChanged;
        viewModel.StateChanged += HandleStateChanged;
        options.Changed += HandleOptionsChanged;

        if (viewModel.IsFasting)
        {
            timer.Start();
        }

        _ = PersistStateAsync();
    }

    public string Id => "Fasting";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Health;

    public int Order => 15;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object BackgroundContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public uint? FooterForegroundColor => 0xFFFDF8FF;

    public bool IsAttentionEnabledByDefault => true;

    public event EventHandler? FooterAppearanceChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() => [
        new GlanceActionDescriptor("Fasting.Start", Id, "Start fast", "Start a fast using the preferred plan.")
        {
            SemanticTags = ["fast", "fasting", "health", "start"],
            ExampleUtterances = ["start my fast", "begin fasting"]
        },
        new GlanceActionDescriptor("Fasting.Stop", Id, "Stop fast", "Stop the current fast without completing it.")
        {
            SemanticTags = ["fast", "fasting", "health", "stop", "cancel"],
            ExampleUtterances = ["stop my fast", "end fasting"]
        },
        new GlanceActionDescriptor("Fasting.Reset", Id, "Reset fast", "Clear the current or completed fast.")
        {
            SemanticTags = ["fast", "fasting", "health", "reset"],
            ExampleUtterances = ["reset my fast"]
        }
    ];

    public bool IsAvailable(string actionId) => actionId switch
    {
        "Fasting.Start" => !viewModel.IsFasting,
        "Fasting.Stop" => viewModel.IsFasting,
        _ => true
    };

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request, CancellationToken cancellationToken = default)
    {
        switch (request.ActionId)
        {
            case "Fasting.Start" when !viewModel.IsFasting:
                viewModel.Start(options.Current, timeProvider.GetLocalNow());
                break;
            case "Fasting.Stop" when viewModel.IsFasting:
                viewModel.Stop(timeProvider.GetLocalNow());
                break;
            case "Fasting.Reset":
                Reset();
                break;
            default:
                return Task.FromResult(GlanceActionResult.Unavailable());
        }

        return Task.FromResult(GlanceActionResult.Success());
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTimerTick;
        viewModel.PropertyChanged -= HandlePropertyChanged;
        viewModel.StateChanged -= HandleStateChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private void Toggle()
    {
        if (viewModel.IsFasting)
        {
            viewModel.Stop(timeProvider.GetLocalNow());
        }
        else
        {
            viewModel.Start(options.Current, timeProvider.GetLocalNow());
        }
    }

    private void Reset() => viewModel.Reset(timeProvider.GetLocalNow());

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(FastingViewModel.IsFasting))
        {
            return;
        }

        if (viewModel.IsFasting)
        {
            timer.Start();
        }
        else
        {
            timer.Stop();
        }
    }

    private void HandleTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!viewModel.Refresh(timeProvider.GetLocalNow()) || viewModel.CompletionAttentionSent)
        {
            return;
        }

        attentionService.RequestAttention(Id);
        viewModel.MarkCompletionAttentionSent();
    }

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<FastingSettings> args) => _ = dispatcherQueue.TryEnqueue(() => viewModel.ApplySettings(args.Options, timeProvider.GetLocalNow()));

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
