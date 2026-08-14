using Glance.Application.Abstractions;
using Glance.Spotify;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Spotify.WinUI;

public sealed class SpotifyComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly SpotifyViewModel viewModel;
    private readonly ISpotifyConnectionService connectionService;
    private readonly ISpotifyPlaybackService playbackService;
    private readonly ModuleResourceTextLocalizer<SpotifyModule> localizer;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly DispatcherQueueTimer refreshTimer;
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken token;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private DateTimeOffset refreshAfter;
    private int refreshCount;
    private int refreshing;
    private int disposed;

    public SpotifyComponent(SpotifyViewModel viewModel,
        ISpotifyConnectionService connectionService,
        ISpotifyPlaybackService playbackService,
        ModuleResourceTextLocalizer<SpotifyModule> localizer)
    {
        this.viewModel = viewModel;
        this.connectionService = connectionService;
        this.playbackService = playbackService;
        this.localizer = localizer;
        token = cancellation.Token;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        SpotifyCompactView compactView = new(viewModel);
        SpotifyExpandedView expandedView = new(viewModel);
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        refreshTimer = dispatcherQueue.CreateTimer();
        refreshTimer.Interval = TimeSpan.FromSeconds(2);
        refreshTimer.IsRepeating = true;
        refreshTimer.Tick += HandleRefreshTimerTick;
        connectionService.StateChanged += HandleConnectionStateChanged;
        viewModel.PlaybackActionRequested += HandlePlaybackActionRequested;
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        _ = ConfigureAsync();
    }

    public string Id => "Spotify";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public int Order => 25;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

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

            await RefreshOnceAsync(cancellationToken);
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
        connectionService.StateChanged -= HandleConnectionStateChanged;
        viewModel.PlaybackActionRequested -= HandlePlaybackActionRequested;
        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task ConfigureAsync()
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        await synchronization.WaitAsync(token);

        try
        {
            refreshTimer.Stop();

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

            if (restored)
            {
                await RefreshOnceAsync(token);
                refreshTimer.Start();
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

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (connectionService.State != SpotifyConnectionState.Connected)
        {
            return;
        }

        if (refreshAfter > DateTimeOffset.UtcNow)
        {
            return;
        }

        try
        {
            SpotifyPlaybackSnapshot? playback = await playbackService.GetPlaybackAsync(cancellationToken);
            IReadOnlyList<SpotifyDevice>? devices = null;

            if (refreshCount++ % 5 == 0)
            {
                devices = await playbackService.GetDevicesAsync(cancellationToken);
            }

            viewModel.ApplyPlayback(playback, devices);
            refreshAfter = DateTimeOffset.MinValue;
        }
        catch (OperationCanceledException)
        {
        }
        catch (SpotifyApiException exception) when (exception.RetryAfter is TimeSpan retryAfter)
        {
            refreshAfter = DateTimeOffset.UtcNow.Add(retryAfter);
            viewModel.StatusMessage = exception.Message;
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref refreshing, 1) != 0)
        {
            return;
        }

        try
        {
            await RefreshAsync(cancellationToken);
        }
        finally
        {
            _ = Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private async void HandleRefreshTimerTick(DispatcherQueueTimer sender, object args) =>
        await RefreshOnceAsync(token);

    private void HandleConnectionStateChanged(object? sender, SpotifyConnectionStateChangedEventArgs args) =>
        _ = dispatcherQueue.TryEnqueue(() =>
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return;
            }

            viewModel.ApplyConnectionState(args.State, args.ErrorMessage);

            if (args.State == SpotifyConnectionState.Connected)
            {
                refreshTimer.Start();
                _ = RefreshOnceAsync(token);
            }
            else
            {
                refreshTimer.Stop();
            }
        });

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SpotifyViewModel.ClientId))
        {
            _ = ConfigureAsync();
        }
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

            await RefreshOnceAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            viewModel.StatusMessage = exception.Message;
        }
    }
}
