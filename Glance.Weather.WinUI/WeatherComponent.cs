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
    private readonly DispatcherQueueTimer sceneClockTimer;
    private readonly DispatcherQueueTimer settingsTimer;
    private readonly GlanceModuleOptions<WeatherSettings> options;
    private readonly ITextLocalizer localizer;
    private readonly IWeatherService weatherService;
    private readonly WeatherViewModel viewModel;
    private CancellationTokenSource? refreshCancellation;
    private WeatherSnapshot? lastSnapshot;
    private string refreshApiKey;
    private string refreshLocation;
    private bool refreshUseFahrenheit;

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
        refreshApiKey = options.Current.ApiKey;
        refreshLocation = options.Current.Location;
        refreshUseFahrenheit = options.Current.UseFahrenheit;

        WeatherCompactView compactView = new(viewModel);
        WeatherExpandedView expandedView = new(viewModel);
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

        sceneClockTimer = dispatcherQueue.CreateTimer();
        sceneClockTimer.Interval = TimeSpan.FromSeconds(10);
        sceneClockTimer.IsRepeating = true;
        sceneClockTimer.Tick += HandleSceneClockTimer;
        sceneClockTimer.Start();

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
        sceneClockTimer.Stop();
        sceneClockTimer.Tick -= HandleSceneClockTimer;
        settingsTimer.Stop();
        settingsTimer.Tick -= HandleSettingsTimer;
        options.Changed -= HandleOptionsChanged;
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
    }

    private void HandleRefreshTimer(DispatcherQueueTimer sender, object args) => Refresh();

    private void HandleSceneClockTimer(DispatcherQueueTimer sender, object args) => ApplySceneOverride();

    private void HandleSettingsTimer(DispatcherQueueTimer sender, object args) => Refresh();

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<WeatherSettings> args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            ApplySceneOverride();

            if (RequiresRefresh(args.Options))
            {
                settingsTimer.Stop();
                settingsTimer.Start();
            }
        });

    private bool RequiresRefresh(WeatherSettings settings)
    {
        bool changed = !string.Equals(refreshApiKey, settings.ApiKey, StringComparison.Ordinal) ||
            !string.Equals(refreshLocation, settings.Location, StringComparison.Ordinal) ||
            refreshUseFahrenheit != settings.UseFahrenheit;
        refreshApiKey = settings.ApiKey;
        refreshLocation = settings.Location;
        refreshUseFahrenheit = settings.UseFahrenheit;
        return changed;
    }

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
                    lastSnapshot = snapshot;
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
        WeatherSettings settings = options.Current;
        WeatherTimeOfDay time = lastSnapshot?.TimeOfDay ?? WeatherTimeOfDay.Afternoon;
        WeatherSky sky = lastSnapshot?.Sky ?? WeatherSky.Clear;
        WeatherEffect effect = lastSnapshot?.Effect ?? WeatherEffect.None;
        WeatherTemperature temperature = lastSnapshot?.TemperatureState ?? WeatherTemperature.Normal;
        double hour = 14;
        double sunrise = 6;
        double sunset = 19;

        if (lastSnapshot is not null)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            hour = WeatherConditionMapper.GetLocalHour(timestamp, lastSnapshot.TimeZoneOffset);
            sunrise = WeatherConditionMapper.GetLocalHour(lastSnapshot.Sunrise, lastSnapshot.TimeZoneOffset);
            sunset = WeatherConditionMapper.GetLocalHour(lastSnapshot.Sunset, lastSnapshot.TimeZoneOffset);
            time = WeatherConditionMapper.MapTime(timestamp, lastSnapshot.Sunrise, lastSnapshot.Sunset);
        }

        WeatherCelestial celestial = WeatherConditionMapper.MapCelestial(time, sky);

#if DEBUG
        if (settings.DebugPreviewEnabled)
        {
            hour = Math.Clamp(settings.PreviewHour, 0, 24) % 24;
            sunrise = 6;
            sunset = 19;
            time = WeatherConditionMapper.MapTime(hour, sunrise, sunset);
            sky = settings.PreviewSky == WeatherSky.Live ? WeatherSky.PartlyCloudy : settings.PreviewSky;
            celestial = settings.PreviewCelestial == WeatherCelestial.Live ?
                WeatherConditionMapper.MapCelestial(time, sky) :
                settings.PreviewCelestial;
            effect = settings.PreviewEffect == WeatherEffect.Live ? WeatherEffect.None : settings.PreviewEffect;
            temperature = settings.PreviewTemperature == WeatherTemperature.Live ? WeatherTemperature.Normal : settings.PreviewTemperature;
        }
#endif

        viewModel.SetVisualState(time, sky, celestial, effect, temperature, hour, sunrise, sunset);
    }
}
