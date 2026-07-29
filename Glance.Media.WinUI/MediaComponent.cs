using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Glance.Media.WinUI;

public sealed partial class MediaComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceAttentionComponent,
    IDisposable
{
    private const int ArtworkMissingDelayMs = 700;
    private const int MediaRefreshDelayMs = 120;
    private const int SessionMissingDelayMs = 700;

    private static readonly double[] SilentAudioLevels = [0, 0, 0, 0, 0];

    private readonly MediaViewModel viewModel;
    private readonly ITextLocalizer localizer;
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly AudioLevelMonitor audioLevelMonitor;
    private DispatcherQueueTimer? artworkMissingTimer;
    private DispatcherQueueTimer? mediaRefreshTimer;
    private DispatcherQueueTimer? sessionMissingTimer;
    private GlobalSystemMediaTransportControlsSessionManager? sessionManager;
    private GlobalSystemMediaTransportControlsSession? session;
    private string? currentArtworkHash;
    private string? currentTitle;
    private int refreshGeneration;

    public MediaComponent(MediaViewModel viewModel,
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
        viewModel.PropertyChanged += HandleViewModelPropertyChanged;
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

    public bool IsAttentionEnabledByDefault => false;

    public void Dispose()
    {
        viewModel.PlaybackActionRequested -= HandlePlaybackActionRequested;
        viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        audioLevelMonitor.LevelsChanged -= HandleAudioLevelsChanged;
        audioLevelMonitor.Dispose();
        (viewModel.AmbientArtwork as IDisposable)?.Dispose();
        refreshGeneration++;

        if (sessionManager is not null)
        {
            sessionManager.CurrentSessionChanged -= HandleCurrentSessionChanged;
        }

        if (mediaRefreshTimer is not null)
        {
            mediaRefreshTimer.Stop();
            mediaRefreshTimer.Tick -= HandleMediaRefreshTimerTick;
        }

        if (artworkMissingTimer is not null)
        {
            artworkMissingTimer.Stop();
            artworkMissingTimer.Tick -= HandleArtworkMissingTimerTick;
        }

        if (sessionMissingTimer is not null)
        {
            sessionMissingTimer.Stop();
            sessionMissingTimer.Tick -= HandleSessionMissingTimerTick;
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

    private void HandleCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(() => UpdateCurrentSession(sender.GetCurrentSession()));

    private async void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? newSession)
    {
        if (newSession is null)
        {
            artworkMissingTimer?.Stop();
            mediaRefreshTimer?.Stop();
            DetachSession();
            ScheduleSessionMissingCheck();
            return;
        }

        sessionMissingTimer?.Stop();
        AttachSession(newSession);
        await Refresh();
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? newSession)
    {
        artworkMissingTimer?.Stop();
        mediaRefreshTimer?.Stop();
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
        refreshGeneration++;

        if (session is not null)
        {
            session.MediaPropertiesChanged -= HandleMediaPropertiesChanged;
            session.PlaybackInfoChanged -= HandlePlaybackInfoChanged;
            session = null;
        }
    }

    private void HandleMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(ScheduleMediaRefresh);

    private void ScheduleMediaRefresh()
    {
        mediaRefreshTimer ??= CreateMediaRefreshTimer();
        mediaRefreshTimer.Stop();
        mediaRefreshTimer.Start();
    }

    private DispatcherQueueTimer CreateMediaRefreshTimer()
    {
        DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(MediaRefreshDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleMediaRefreshTimerTick;
        return timer;
    }

    private async void HandleMediaRefreshTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        await Refresh();
    }

    private void ScheduleArtworkMissingCheck()
    {
        artworkMissingTimer ??= CreateArtworkMissingTimer();
        artworkMissingTimer.Stop();
        artworkMissingTimer.Start();
    }

    private DispatcherQueueTimer CreateArtworkMissingTimer()
    {
        DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ArtworkMissingDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleArtworkMissingTimerTick;
        return timer;
    }

    private async void HandleArtworkMissingTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        await Refresh(allowArtworkClear: true);
    }

    private void ScheduleSessionMissingCheck()
    {
        sessionMissingTimer ??= CreateSessionMissingTimer();
        sessionMissingTimer.Stop();
        sessionMissingTimer.Start();
    }

    private DispatcherQueueTimer CreateSessionMissingTimer()
    {
        DispatcherQueueTimer timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(SessionMissingDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleSessionMissingTimerTick;
        return timer;
    }

    private async void HandleSessionMissingTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        GlobalSystemMediaTransportControlsSession? currentSession = sessionManager?.GetCurrentSession();

        if (currentSession is not null)
        {
            AttachSession(currentSession);
            await Refresh();
            return;
        }

        ShowEmptyState();
    }

    private void HandlePlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) =>
        dispatcherQueue.TryEnqueue(RefreshPlaybackState);

    private void HandleAudioLevelsChanged(object? sender,
        AudioSpectrumEventArgs args) =>
        dispatcherQueue.TryEnqueue(() => viewModel.UpdateAudioLevels(args.Levels));

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MediaViewModel.ShowAudioVisualization))
        {
            UpdateAudioCaptureState();
        }
    }

    private async void HandlePlaybackActionRequested(object? sender,
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

    private async Task Refresh(bool allowArtworkClear = false)
    {
        int generation = ++refreshGeneration;
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

        string title = string.IsNullOrWhiteSpace(properties.Title) ? localizer.GetText("UnknownTrack") : properties.Title;
        string artist = string.IsNullOrWhiteSpace(properties.Artist) ? localizer.GetText("UnknownArtist") : properties.Artist;
        string source = FormatSourceName(mediaSession.SourceAppUserModelId);
        GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo =
            mediaSession.GetPlaybackInfo();
        IRandomAccessStreamWithContentType? artworkStream = null;
        string? artworkHash = null;

        if (properties.Thumbnail is not null)
        {
            try
            {
                artworkStream = await properties.Thumbnail.OpenReadAsync();
                artworkHash = await CalculateArtworkHash(artworkStream);
            }
            catch
            {
                artworkStream?.Dispose();
                artworkStream = null;
            }
        }

        try
        {
            if (generation != refreshGeneration || !ReferenceEquals(mediaSession, session))
            {
                return;
            }

            await RunOnDispatcherAsync(async () =>
            {
                if (generation != refreshGeneration || !ReferenceEquals(mediaSession, session))
                {
                    return;
                }

                viewModel.Title = title;
                viewModel.Artist = artist;
                viewModel.Source = source;
                viewModel.HasSession = true;
                ApplyPlaybackInfo(playbackInfo);

                bool preserveArtwork = artworkStream is null &&
                    currentArtworkHash is not null &&
                    !allowArtworkClear;

                if (preserveArtwork)
                {
                    ScheduleArtworkMissingCheck();
                }
                else
                {
                    artworkMissingTimer?.Stop();

                    if (!string.Equals(currentArtworkHash, artworkHash, StringComparison.Ordinal))
                    {
                        if (artworkStream is not null)
                        {
                            BitmapImage artwork = new();
                            await artwork.SetSourceAsync(artworkStream);
                            MediaAmbientArtwork? ambientArtwork = null;

                            try
                            {
                                artworkStream.Seek(0);
                                IRandomAccessStream ambientStream = artworkStream;
                                artworkStream = null;
                                ambientArtwork = await MediaAmbientArtwork.LoadAsync(ambientStream);
                            }
                            catch
                            {
                            }

                            viewModel.Artwork = artwork;

                            if (ambientArtwork is not null)
                            {
                                viewModel.AmbientArtwork = ambientArtwork;
                            }
                        }
                        else
                        {
                            viewModel.Artwork = null;
                            viewModel.AmbientArtwork = null;
                        }

                        currentArtworkHash = artworkHash;
                    }
                }

                if (currentTitle is not null &&
                    !string.Equals(currentTitle, title, StringComparison.Ordinal))
                {
                    attentionService.RequestAttention(Id, GlanceAttentionLevel.Passive, expand: false);
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
        artworkMissingTimer?.Stop();
        currentArtworkHash = null;
        currentTitle = null;
        viewModel.Title = localizer.GetText("NothingPlaying");
        viewModel.Artist = localizer.GetText("OpenMediaApp");
        viewModel.Source = localizer.GetText("ModuleTitle");
        viewModel.Artwork = null;
        viewModel.AmbientArtwork = null;
        viewModel.IsPlaying = false;
        viewModel.HasSession = false;
        viewModel.CanSkipPrevious = false;
        viewModel.CanSkipNext = false;
        viewModel.CanTogglePlayback = false;
        UpdateAudioCaptureState();
    }

    private static async Task<string> CalculateArtworkHash(IRandomAccessStream stream)
    {
        if (stream.Size > int.MaxValue)
        {
            throw new InvalidOperationException();
        }

        using DataReader reader = new(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        byte[] bytes = new byte[(int)stream.Size];
        reader.ReadBytes(bytes);
        stream.Seek(0);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private void ApplyPlaybackInfo(GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo)
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
        if (viewModel.ShowAudioVisualization && viewModel.HasSession && viewModel.IsPlaying)
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
            completion.SetException(new InvalidOperationException("The media dispatcher rejected an update."));
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

        return source.Replace("exe", string.Empty, StringComparison.OrdinalIgnoreCase).TrimEnd('.');
    }
}
