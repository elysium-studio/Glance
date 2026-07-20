using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Glance.Media.WinUI;

public sealed class MediaComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private static readonly double[] SilentAudioLevels = [0, 0, 0, 0, 0];

    private readonly MediaViewModel viewModel;
    private readonly ITextLocalizer localizer;
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly AudioLevelMonitor audioLevelMonitor;
    private GlobalSystemMediaTransportControlsSessionManager? sessionManager;
    private GlobalSystemMediaTransportControlsSession? session;
    private string? currentTitle;

    public MediaComponent(
        MediaViewModel viewModel,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<MediaModule> localizer)
    {
        this.viewModel = viewModel;
        this.attentionService = attentionService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        audioLevelMonitor = new AudioLevelMonitor();

        MediaCompactView compactView = new(viewModel);
        MediaExpandedView expandedView = new(viewModel);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.PlaybackActionRequested += HandlePlaybackActionRequested;
        audioLevelMonitor.LevelsChanged += HandleAudioLevelsChanged;
        Initialize();
    }

    public string Id => "Media";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 20;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        viewModel.PlaybackActionRequested -= HandlePlaybackActionRequested;
        audioLevelMonitor.LevelsChanged -= HandleAudioLevelsChanged;
        audioLevelMonitor.Dispose();

        if (sessionManager is not null)
        {
            sessionManager.CurrentSessionChanged -= HandleCurrentSessionChanged;
        }

        DetachSession();
    }

    private async void Initialize()
    {
        try
        {
            sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            sessionManager.CurrentSessionChanged += HandleCurrentSessionChanged;
            AttachSession(sessionManager.GetCurrentSession());
            await Refresh();
        }
        catch
        {
            await RunOnDispatcherAsync(() =>
            {
                ShowEmptyState();
                return Task.CompletedTask;
            });
        }
    }

    private void HandleCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(async () =>
        {
            AttachSession(sender.GetCurrentSession());
            await Refresh();
        });

    private void AttachSession(GlobalSystemMediaTransportControlsSession? newSession)
    {
        DetachSession();
        session = newSession;

        if (session is not null)
        {
            session.MediaPropertiesChanged += HandleMediaPropertiesChanged;
            session.PlaybackInfoChanged += HandlePlaybackInfoChanged;
        }
    }

    private void DetachSession()
    {
        if (session is not null)
        {
            session.MediaPropertiesChanged -= HandleMediaPropertiesChanged;
            session.PlaybackInfoChanged -= HandlePlaybackInfoChanged;
            session = null;
        }
    }

    private void HandleMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(async () => await Refresh());

    private void HandlePlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(RefreshPlaybackState);

    private void HandleAudioLevelsChanged(
        object? sender,
        AudioSpectrumEventArgs args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.UpdateAudioLevels(args.Levels));

    private async void HandlePlaybackActionRequested(
        object? sender,
        MediaPlaybackAction action)
    {
        if (session is null)
        {
            return;
        }

        switch (action)
        {
            case MediaPlaybackAction.Previous:
                await session.TrySkipPreviousAsync();
                break;
            case MediaPlaybackAction.TogglePlayback:
                await session.TryTogglePlayPauseAsync();
                break;
            case MediaPlaybackAction.Next:
                await session.TrySkipNextAsync();
                break;
        }
    }

    private async Task Refresh()
    {
        GlobalSystemMediaTransportControlsSession? mediaSession = session;

        if (mediaSession is null)
        {
            await RunOnDispatcherAsync(() =>
            {
                ShowEmptyState();
                return Task.CompletedTask;
            });
            return;
        }

        GlobalSystemMediaTransportControlsSessionMediaProperties properties =
            await mediaSession.TryGetMediaPropertiesAsync();

        string title = string.IsNullOrWhiteSpace(properties.Title)
            ? localizer.GetText("UnknownTrack")
            : properties.Title;
        string artist = string.IsNullOrWhiteSpace(properties.Artist)
            ? localizer.GetText("UnknownArtist")
            : properties.Artist;
        string source = FormatSourceName(mediaSession.SourceAppUserModelId);
        GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo =
            mediaSession.GetPlaybackInfo();
        IRandomAccessStreamWithContentType? artworkStream = null;

        if (properties.Thumbnail is not null)
        {
            try
            {
                artworkStream = await properties.Thumbnail.OpenReadAsync();
            }
            catch
            {
            }
        }

        try
        {
            await RunOnDispatcherAsync(async () =>
            {
                viewModel.Title = title;
                viewModel.Artist = artist;
                viewModel.Source = source;
                viewModel.HasSession = true;
                viewModel.Artwork = null;
                ApplyPlaybackInfo(playbackInfo);

                if (artworkStream is not null)
                {
                    BitmapImage artwork = new();
                    viewModel.Artwork = artwork;
                    await artwork.SetSourceAsync(artworkStream);
                }

                if (currentTitle is not null &&
                    !string.Equals(currentTitle, title, StringComparison.Ordinal))
                {
                    attentionService.RequestAttention(
                        Id,
                        GlanceAttentionLevel.Passive,
                        expand: false);
                }

                currentTitle = title;
            });
        }
        finally
        {
            artworkStream?.Dispose();
        }
    }

    private void RefreshPlaybackState()
    {
        GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo =
            session?.GetPlaybackInfo();

        viewModel.HasSession = session is not null;
        ApplyPlaybackInfo(playbackInfo);
    }

    private void ShowEmptyState()
    {
        currentTitle = null;
        viewModel.Title = localizer.GetText("NothingPlaying");
        viewModel.Artist = localizer.GetText("OpenMediaApp");
        viewModel.Source = localizer.GetText("ModuleTitle");
        viewModel.Artwork = null;
        viewModel.IsPlaying = false;
        viewModel.HasSession = false;
        viewModel.CanSkipPrevious = false;
        viewModel.CanSkipNext = false;
        viewModel.CanTogglePlayback = false;
        UpdateAudioCaptureState();
    }

    private void ApplyPlaybackInfo(
        GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo)
    {
        GlobalSystemMediaTransportControlsSessionPlaybackControls? controls =
            playbackInfo?.Controls;

        viewModel.IsPlaying = playbackInfo?.PlaybackStatus ==
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        viewModel.CanSkipPrevious = viewModel.HasSession &&
            controls?.IsPreviousEnabled == true;
        viewModel.CanSkipNext = viewModel.HasSession &&
            controls?.IsNextEnabled == true;
        viewModel.CanTogglePlayback = viewModel.HasSession &&
            (controls?.IsPlayEnabled == true || controls?.IsPauseEnabled == true);
        UpdateAudioCaptureState();
    }

    private void UpdateAudioCaptureState()
    {
        if (viewModel.HasSession && viewModel.IsPlaying)
        {
            if (!audioLevelMonitor.Start())
            {
                viewModel.UpdateAudioLevels(SilentAudioLevels);
            }

            return;
        }

        audioLevelMonitor.Stop();
        viewModel.UpdateAudioLevels(SilentAudioLevels);
    }

    private Task RunOnDispatcherAsync(Func<Task> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

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
            completion.SetException(new InvalidOperationException(
                "The media dispatcher rejected an update."));
        }

        return completion.Task;
    }

    private static string FormatSourceName(string sourceAppUserModelId)
    {
        string source = sourceAppUserModelId.Split('!')[0];
        int finalSeparator = source.LastIndexOfAny(['.', '\\']);

        if (finalSeparator >= 0 && finalSeparator < source.Length - 1)
        {
            source = source[(finalSeparator + 1)..];
        }

        return source.Replace("exe", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('.');
    }
}
