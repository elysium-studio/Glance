using Glance.Application.Abstractions;
using Glance.Spotify;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Glance.Spotify.WinUI;

public sealed class SpotifyComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceBackgroundComponent,
    IGlanceFooterAppearanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceViewAwareComponent,
    IDisposable
{
    private static readonly TimeSpan PlaybackRefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DeviceRefreshInterval = TimeSpan.FromMinutes(1);
    private readonly SpotifyViewModel viewModel;
    private readonly ISpotifyConnectionService connectionService;
    private readonly ISpotifyPlaybackService playbackService;
    private readonly ModuleResourceTextLocalizer<SpotifyModule> localizer;
    private readonly HttpClient httpClient;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer rateLimitTimer;
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly SpotifyAlbumAmbience backgroundView;
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken token;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private DateTimeOffset refreshAfter;
    private DateTimeOffset nextPlaybackRefreshAt;
    private DateTimeOffset nextDeviceRefreshAt;
    private DateTimeOffset lastProgressUpdateAt;
    private int refreshing;
    private int artworkGeneration;
    private int disposed;
    private int isInView;
    private CancellationTokenSource? artworkCancellation;
    private SpotifyAmbientArtwork? currentAmbientArtwork;
    private uint currentArtworkAverageColor = 0xFF2C2C2C;

    public SpotifyComponent(SpotifyViewModel viewModel,
        ISpotifyConnectionService connectionService,
        ISpotifyPlaybackService playbackService,
        ModuleResourceTextLocalizer<SpotifyModule> localizer,
        HttpClient httpClient)
    {
        this.viewModel = viewModel;
        this.connectionService = connectionService;
        this.playbackService = playbackService;
        this.localizer = localizer;
        this.httpClient = httpClient;
        token = cancellation.Token;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        backgroundView = new SpotifyAlbumAmbience { ViewModel = viewModel };
        SpotifyCompactView compactView = new(viewModel);
        SpotifyExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        BackgroundContent = backgroundView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        refreshTimer = dispatcherQueue.CreateTimer();
        refreshTimer.Interval = TimeSpan.FromSeconds(1);
        refreshTimer.IsRepeating = true;
        refreshTimer.Tick += HandleRefreshTimerTick;
        rateLimitTimer = dispatcherQueue.CreateTimer();
        rateLimitTimer.IsRepeating = false;
        rateLimitTimer.Tick += HandleRateLimitTimerTick;
        connectionService.StateChanged += HandleConnectionStateChanged;
        viewModel.PlaybackActionRequested += HandlePlaybackActionRequested;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        backgroundView.SurfaceAppearanceChanged += HandleBackgroundSurfaceAppearanceChanged;
    }

    public string Id => "Spotify";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public object CreateIcon(bool isLightTheme) => new ImageIcon
    {
        Width = 24,
        Height = 24,
        Source = SpotifyLogo.CreateImageSource(isLightTheme, 24)
    };

    public int Order => 25;

    public object CompactContent { get; }

    public object BackgroundContent { get; }

    public object ExpandedContent { get; }

    public uint? FooterForegroundColor => !viewModel.HasPlayback ||
        viewModel.AmbientArtwork is null ? null : viewModel.BackgroundForegroundColor;

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public event EventHandler? FooterAppearanceChanged;

    public void EnterView()
    {
        if (Volatile.Read(ref disposed) != 0 || Interlocked.Exchange(ref isInView, 1) != 0)
        {
            return;
        }

        if (connectionService.State == SpotifyConnectionState.Connected &&
            refreshAfter > DateTimeOffset.UtcNow)
        {
            SuspendForRateLimit();
            return;
        }

        _ = ConfigureAsync();
    }

    public void LeaveView()
    {
        if (Interlocked.Exchange(ref isInView, 0) == 0)
        {
            return;
        }

        refreshTimer.Stop();
        rateLimitTimer.Stop();
        lastProgressUpdateAt = default;
    }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("Spotify.Previous", Id, "Previous Spotify track", "Return to the previous item in Spotify."),
        new GlanceActionDescriptor("Spotify.Play", Id, "Play Spotify", "Resume Spotify playback."),
        new GlanceActionDescriptor("Spotify.Pause", Id, "Pause Spotify", "Pause Spotify playback."),
        new GlanceActionDescriptor("Spotify.Next", Id, "Next Spotify track", "Skip to the next item in Spotify.")
    ];

    public bool IsAvailable(string actionId) => viewModel.IsConnected && viewModel.CanControlPlayback;

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable(request.ActionId))
        {
            return GlanceActionResult.Unavailable(localizer.GetText("ConnectInSettings"));
        }

        try
        {
            switch (request.ActionId)
            {
                case "Spotify.Previous":
                    await playbackService.PreviousAsync(cancellationToken);
                    break;
                case "Spotify.Play":
                    await playbackService.ResumeAsync(cancellationToken);
                    break;
                case "Spotify.Pause":
                    await playbackService.PauseAsync(cancellationToken);
                    break;
                case "Spotify.Next":
                    await playbackService.NextAsync(cancellationToken);
                    break;
                default:
                    return GlanceActionResult.Unavailable();
            }

            await RefreshOnceAsync(cancellationToken, true);
            return GlanceActionResult.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return GlanceActionResult.Failed(exception.Message);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        refreshTimer.Stop();
        refreshTimer.Tick -= HandleRefreshTimerTick;
        rateLimitTimer.Stop();
        rateLimitTimer.Tick -= HandleRateLimitTimerTick;
        connectionService.StateChanged -= HandleConnectionStateChanged;
        viewModel.PlaybackActionRequested -= HandlePlaybackActionRequested;
        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        backgroundView.SurfaceAppearanceChanged -= HandleBackgroundSurfaceAppearanceChanged;
        Interlocked.Increment(ref artworkGeneration);
        artworkCancellation?.Cancel();
        artworkCancellation?.Dispose();
        artworkCancellation = null;
        SetAmbientArtwork(null);
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task ConfigureAsync()
    {
        if (Volatile.Read(ref disposed) != 0 || Volatile.Read(ref isInView) == 0)
        {
            return;
        }

        await synchronization.WaitAsync(token);

        try
        {
            refreshTimer.Stop();
            rateLimitTimer.Stop();

            if (Volatile.Read(ref isInView) == 0)
            {
                return;
            }

            if (!viewModel.IsConfigured)
            {
                if (connectionService.State == SpotifyConnectionState.Connected)
                {
                    await connectionService.DisconnectAsync(token);
                }

                viewModel.ApplyConnectionState(SpotifyConnectionState.Disconnected);
                return;
            }

            bool restored = await connectionService.RestoreAsync(viewModel.ClientId, token);
            viewModel.ApplyConnectionState(connectionService.State);

            if (restored && Volatile.Read(ref isInView) != 0)
            {
                lastProgressUpdateAt = DateTimeOffset.UtcNow;
                await RefreshOnceAsync(token, true);

                if (refreshAfter > DateTimeOffset.UtcNow)
                {
                    SuspendForRateLimit();
                }
                else if (Volatile.Read(ref isInView) != 0)
                {
                    refreshTimer.Start();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _ = synchronization.Release();
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken,
        bool force)
    {
        if (connectionService.State != SpotifyConnectionState.Connected ||
            Volatile.Read(ref isInView) == 0)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (refreshAfter > now)
        {
            SuspendForRateLimit();
            return;
        }

        if (!force && nextPlaybackRefreshAt > now)
        {
            return;
        }

        try
        {
            SpotifyPlaybackSnapshot? playback = await playbackService.GetPlaybackAsync(cancellationToken);
            viewModel.ApplyPlayback(playback);
            nextPlaybackRefreshAt = DateTimeOffset.UtcNow.Add(PlaybackRefreshInterval);
            refreshAfter = DateTimeOffset.MinValue;
            viewModel.SetStatusMessage(string.Empty);

            await RefreshDevicesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SpotifyApiException exception) when (exception.RetryAfter is TimeSpan retryAfter)
        {
            refreshAfter = DateTimeOffset.UtcNow.Add(retryAfter);
            viewModel.SetStatusMessage(exception.Message);
            SuspendForRateLimit();
        }
        catch (Exception exception)
        {
            viewModel.SetStatusMessage(exception.Message);
        }
    }

    private async Task RefreshDevicesAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (nextDeviceRefreshAt > now)
        {
            return;
        }

        nextDeviceRefreshAt = now.Add(DeviceRefreshInterval);

        try
        {
            IReadOnlyList<SpotifyDevice> devices = await playbackService.GetDevicesAsync(cancellationToken);
            viewModel.ApplyDevices(devices);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SpotifyApiException exception) when (exception.RetryAfter is TimeSpan retryAfter)
        {
            DateTimeOffset retryAt = DateTimeOffset.UtcNow.Add(retryAfter);
            nextDeviceRefreshAt = DateTimeOffset.UtcNow.Add(retryAfter > DeviceRefreshInterval
                ? retryAfter
                : DeviceRefreshInterval);
            refreshAfter = retryAt > refreshAfter ? retryAt : refreshAfter;
            viewModel.SetStatusMessage(exception.Message);
            SuspendForRateLimit();
        }
        catch
        {
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken,
        bool force = false)
    {
        if (Interlocked.Exchange(ref refreshing, 1) != 0)
        {
            return;
        }

        try
        {
            await RefreshAsync(cancellationToken, force);
        }
        finally
        {
            _ = Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private async void HandleRefreshTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (Volatile.Read(ref isInView) == 0)
        {
            sender.Stop();
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (lastProgressUpdateAt != default)
        {
            viewModel.AdvancePlayback(now - lastProgressUpdateAt);
        }

        lastProgressUpdateAt = now;
        await RefreshOnceAsync(token);
    }

    private void SuspendForRateLimit()
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            _ = dispatcherQueue.TryEnqueue(SuspendForRateLimit);
            return;
        }

        refreshTimer.Stop();
        rateLimitTimer.Stop();

        TimeSpan remaining = refreshAfter - DateTimeOffset.UtcNow;

        if (Volatile.Read(ref isInView) == 0 || remaining <= TimeSpan.Zero)
        {
            return;
        }

        rateLimitTimer.Interval = remaining;
        rateLimitTimer.Start();
    }

    private async void HandleRateLimitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (Volatile.Read(ref isInView) == 0 ||
            connectionService.State != SpotifyConnectionState.Connected)
        {
            return;
        }

        refreshAfter = DateTimeOffset.MinValue;
        nextPlaybackRefreshAt = DateTimeOffset.MinValue;
        lastProgressUpdateAt = DateTimeOffset.UtcNow;
        await RefreshOnceAsync(token, true);

        if (refreshAfter <= DateTimeOffset.UtcNow && Volatile.Read(ref isInView) != 0)
        {
            refreshTimer.Start();
        }
    }

    private void HandleConnectionStateChanged(object? sender, SpotifyConnectionStateChangedEventArgs args) =>
        _ = dispatcherQueue.TryEnqueue(() =>
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            viewModel.ApplyConnectionState(args.State, args.ErrorMessage);

            if (args.State == SpotifyConnectionState.Connected && Volatile.Read(ref isInView) != 0)
            {
                nextPlaybackRefreshAt = DateTimeOffset.MinValue;
                nextDeviceRefreshAt = DateTimeOffset.MinValue;
                lastProgressUpdateAt = DateTimeOffset.UtcNow;

                if (refreshAfter > DateTimeOffset.UtcNow)
                {
                    SuspendForRateLimit();
                }
                else
                {
                    refreshTimer.Start();
                    _ = RefreshOnceAsync(token, true);
                }
            }
            else
            {
                refreshTimer.Stop();
                rateLimitTimer.Stop();
            }
        });

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SpotifyViewModel.ClientId))
        {
            if (Volatile.Read(ref isInView) != 0)
            {
                _ = ConfigureAsync();
            }
        }

        if (args.PropertyName == nameof(SpotifyViewModel.ArtworkUrl))
        {
            QueueArtworkUpdate(viewModel.ArtworkUrl);
        }

        if (args.PropertyName is nameof(SpotifyViewModel.AccentColor) or
            nameof(SpotifyViewModel.BackgroundForegroundColor) or
            nameof(SpotifyViewModel.AmbientArtwork) or
            nameof(SpotifyViewModel.HasPlayback))
        {
            FooterAppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleBackgroundSurfaceAppearanceChanged(object? sender, EventArgs args)
    {
        if (viewModel.HasPlayback && viewModel.AmbientArtwork is not null)
        {
            viewModel.BackgroundForegroundColor =
                backgroundView.GetContrastingForeground(currentArtworkAverageColor);
        }
    }

    private void QueueArtworkUpdate(string? artworkUrl)
    {
        int generation = Interlocked.Increment(ref artworkGeneration);
        artworkCancellation?.Cancel();
        artworkCancellation?.Dispose();
        artworkCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _ = UpdateArtworkAsync(artworkUrl, generation, artworkCancellation.Token);
    }

    private async Task UpdateArtworkAsync(string? artworkUrl,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(artworkUrl))
            {
                await RunOnDispatcherAsync(() =>
                {
                    if (generation == Volatile.Read(ref artworkGeneration))
                    {
                        ClearArtwork();
                    }

                    return Task.CompletedTask;
                });
                return;
            }

            using HttpResponseMessage response = await httpClient.GetAsync(artworkUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            SpotifyArtworkColors colors;

            using (InMemoryRandomAccessStream analysisStream = await CreateStreamAsync(bytes))
            {
                colors = await SpotifyArtworkColorAnalyzer.AnalyzeAsync(analysisStream);
            }

            await RunOnDispatcherAsync(async () =>
            {
                if (generation != Volatile.Read(ref artworkGeneration) ||
                    Volatile.Read(ref disposed) != 0)
                {
                    return;
                }

                BitmapImage artwork = new();

                using (InMemoryRandomAccessStream imageStream = await CreateStreamAsync(bytes))
                {
                    await artwork.SetSourceAsync(imageStream);
                }

                InMemoryRandomAccessStream ambientStream = await CreateStreamAsync(bytes);
                SpotifyAmbientArtwork? ambientArtwork = await SpotifyAmbientArtwork.LoadAsync(ambientStream);

                if (generation != Volatile.Read(ref artworkGeneration) ||
                    Volatile.Read(ref disposed) != 0)
                {
                    ambientArtwork?.Dispose();
                    return;
                }

                viewModel.Artwork = artwork;
                viewModel.AccentColor = colors.AccentColor;
                currentArtworkAverageColor = colors.AverageColor;
                viewModel.BackgroundForegroundColor =
                    backgroundView.GetContrastingForeground(colors.AverageColor);
                SetAmbientArtwork(ambientArtwork);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await RunOnDispatcherAsync(() =>
            {
                if (generation == Volatile.Read(ref artworkGeneration))
                {
                    ClearArtwork();
                }

                return Task.CompletedTask;
            });
        }
    }

    private void ClearArtwork()
    {
        viewModel.Artwork = null;
        SetAmbientArtwork(null);
        viewModel.AccentColor = SpotifyViewModel.DefaultAccentColor;
        currentArtworkAverageColor = 0xFF2C2C2C;
        viewModel.BackgroundForegroundColor = 0xFFFFFFFF;
    }

    private void SetAmbientArtwork(SpotifyAmbientArtwork? artwork)
    {
        if (ReferenceEquals(currentAmbientArtwork, artwork))
        {
            return;
        }

        SpotifyAmbientArtwork? previousArtwork = currentAmbientArtwork;
        currentAmbientArtwork = artwork;
        viewModel.AmbientArtwork = artwork;
        previousArtwork?.Dispose();
    }

    private Task RunOnDispatcherAsync(Func<Task> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The Spotify dispatcher rejected an update."));
        }

        return completion.Task;
    }

    private static async Task<InMemoryRandomAccessStream> CreateStreamAsync(byte[] bytes)
    {
        InMemoryRandomAccessStream stream = new();

        using (DataWriter writer = new(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            _ = await writer.StoreAsync();
        }

        stream.Seek(0);
        return stream;
    }

    private async void HandlePlaybackActionRequested(object? sender, SpotifyPlaybackAction action)
    {
        try
        {
            switch (action.Kind)
            {
                case SpotifyPlaybackActionKind.Previous:
                    await playbackService.PreviousAsync(token);
                    break;
                case SpotifyPlaybackActionKind.TogglePlayback:
                    if (viewModel.IsPlaying)
                    {
                        await playbackService.PauseAsync(token);
                    }
                    else
                    {
                        await playbackService.ResumeAsync(token);
                    }
                    break;
                case SpotifyPlaybackActionKind.Next:
                    await playbackService.NextAsync(token);
                    break;
                case SpotifyPlaybackActionKind.Seek:
                    await playbackService.SeekAsync(action.Position, token);
                    break;
                case SpotifyPlaybackActionKind.SetVolume:
                    await playbackService.SetVolumeAsync(action.VolumePercent, token);
                    break;
                case SpotifyPlaybackActionKind.Transfer when action.DeviceId is not null:
                    await playbackService.TransferAsync(action.DeviceId, token);
                    break;
            }

            await RefreshOnceAsync(token, true);
        }
        catch (OperationCanceledException)
        {
            if (action.Kind == SpotifyPlaybackActionKind.Seek)
            {
                viewModel.CancelPendingSeek();
            }
        }
        catch (Exception exception)
        {
            if (action.Kind == SpotifyPlaybackActionKind.Seek)
            {
                viewModel.CancelPendingSeek();
            }

            viewModel.SetStatusMessage(exception.Message);
        }
    }
}
