using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceBackgroundComponent,
    IDisposable
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly DispatcherQueueTimer settingsTimer;
    private readonly GlanceModuleOptions<WeatherSettings> options;
    private readonly ITextLocalizer localizer;
    private readonly IWeatherService weatherService;
    private readonly WeatherViewModel viewModel;
    private CancellationTokenSource? refreshCancellation;

    public WeatherComponent(WeatherViewModel viewModel,
        IWeatherService weatherService,
        GlanceModuleOptions<WeatherSettings> options,
        ModuleResourceTextLocalizer<WeatherModule> localizer)
    {
        this.viewModel = viewModel;
        this.weatherService = weatherService;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        WeatherCompactView compactView = new(viewModel);
        WeatherExpandedView expandedView = new(viewModel, Refresh);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        BackgroundContent = new WeatherScene { ViewModel = viewModel };

        refreshTimer = dispatcherQueue.CreateTimer();
        refreshTimer.Interval = TimeSpan.FromMinutes(10);
        refreshTimer.IsRepeating = true;
        refreshTimer.Tick += HandleRefreshTimer;
        refreshTimer.Start();

        settingsTimer = dispatcherQueue.CreateTimer();
        settingsTimer.Interval = TimeSpan.FromMilliseconds(700);
        settingsTimer.IsRepeating = false;
        settingsTimer.Tick += HandleSettingsTimer;
        options.Changed += HandleOptionsChanged;

        Refresh();
    }

    public string Id => "Weather";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 25;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public object BackgroundContent { get; }

    public void Dispose()
    {
        refreshTimer.Stop();
        refreshTimer.Tick -= HandleRefreshTimer;
        settingsTimer.Stop();
        settingsTimer.Tick -= HandleSettingsTimer;
        options.Changed -= HandleOptionsChanged;
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
    }

    private void HandleRefreshTimer(DispatcherQueueTimer sender, object args) => Refresh();

    private void HandleSettingsTimer(DispatcherQueueTimer sender, object args) => Refresh();

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<WeatherSettings> args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            ApplySceneOverride();
            settingsTimer.Stop();
            settingsTimer.Start();
        });

    private void Refresh() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        WeatherSettings settings = options.Current;

        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Location))
        {
            refreshCancellation?.Cancel();
            viewModel.SetNeedsConfiguration();
            ApplySceneOverride();
            return;
        }

        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = refreshCancellation.Token;
        viewModel.SetLoading();
        ApplySceneOverride();

        try
        {
            WeatherSnapshot snapshot = await weatherService.GetCurrentAsync(settings, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    viewModel.Update(snapshot, settings.UseFahrenheit);
                    ApplySceneOverride();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    viewModel.SetError();
                    ApplySceneOverride();
                });
            }
        }
    }

    private void ApplySceneOverride()
    {
#if DEBUG
        WeatherSceneKind scene = options.Current.PreviewScene;

        if (scene != WeatherSceneKind.Unknown)
        {
            viewModel.Scene = scene;
            viewModel.IsDay = true;
        }
#endif
    }
}
