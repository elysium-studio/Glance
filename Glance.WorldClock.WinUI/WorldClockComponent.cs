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
    IGlanceActionValidator,
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
            "Resolve a spoken city, country, region, or time-zone name and show its current local time.",
            [new GlanceActionParameterDescriptor("city", GlanceActionParameterType.String, "The spoken city, country, region, or time-zone name to resolve.")],
            Presentation: GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["world clock", "clock", "time", "time zone", "city", "country", "local time", "what time"],
            ExampleUtterances = ["what time is it in New York", "show me the time in Greenland", "what's the local time in Tokyo", "open the clock for Pacific time"]
        }
    ];

    public Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        string? city = request.GetString("city");
        return Task.FromResult(city is null
            ? GlanceActionResult.InvalidArguments("A city or time zone is required.")
            : ShowTime(city));
    }

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "WorldClock.ShowTime", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        string? city = request.GetString("city");

        if (string.IsNullOrWhiteSpace(city))
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("Which place do you mean?", "Say a city or time zone."));
        }

        if (viewModel.CanSelectClock(city))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        WorldClockDefinitionResolution resolution = WorldClockTimeZoneCatalog.ResolveDefinition(city, out _);
        return Task.FromResult<GlanceActionResult?>(resolution switch
        {
            WorldClockDefinitionResolution.Resolved => null,
            WorldClockDefinitionResolution.Ambiguous => GlanceActionResult.InvalidArguments($"Several places match “{city}”.", "Try a city or more specific time zone."),
            _ => GlanceActionResult.InvalidArguments($"I couldn't find “{city}”.", "Try another city or time zone.")
        });
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
