using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer timer;
    private readonly ITextLocalizer localizer;
    private readonly GlanceModuleOptions<WorldClockSettings> options;
    private readonly TimeProvider timeProvider;
    private readonly WorldClockViewModel viewModel;

    public WorldClockComponent(WorldClockViewModel viewModel,
        TimeProvider timeProvider,
        GlanceModuleOptions<WorldClockSettings> options,
        ModuleResourceTextLocalizer<WorldClockModule> localizer)
    {
        this.viewModel = viewModel;
        this.timeProvider = timeProvider;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        WorldClockCompactView compactView = new(viewModel);
        WorldClockExpandedView expandedView = new(viewModel, localizer);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();
        options.Changed += HandleOptionsChanged;
        Refresh();
    }

    public string Id => "WorldClock";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 15;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("WorldClock.ShowTime",
            Id,
            "Show time in a city",
            "Select a world clock and bring it into view.",
            [new GlanceActionParameterDescriptor("city", GlanceActionParameterType.String, "The city or time zone to display.")],
            Presentation: GlanceActionPresentation.Expanded)
    ];

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? city = request.GetString("city");
        return Task.FromResult(city is null
            ? GlanceActionResult.InvalidArguments("A city or time zone is required.")
            : ShowTime(city));
    }

    public GlanceActionResult ShowTime(string city)
    {
        if (!viewModel.SelectClock(city))
        {
            if (!WorldClockTimeZoneCatalog.TryCreateDefinition(city, out WorldClockDefinition? definition) || definition is null)
            {
                return GlanceActionResult.InvalidArguments($"I couldn't find a time zone for {city}.");
            }

            viewModel.ShowClock(definition);
        }

        Refresh();
        return GlanceActionResult.Success($"Showing the time for {viewModel.SelectedClock?.DisplayName}.");
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        options.Changed -= HandleOptionsChanged;
    }

    private void HandleTick(DispatcherQueueTimer sender, object args) => Refresh();

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<WorldClockSettings> args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            viewModel.SetClocks(WorldClockTimeZoneCatalog.CreateDefinitions(args.Options, localizer));
            Refresh();
        });

    private void Refresh() => viewModel.Refresh(timeProvider.GetUtcNow(), options.Current.Use24HourTime);
}
