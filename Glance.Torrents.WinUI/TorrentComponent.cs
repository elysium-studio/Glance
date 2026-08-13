using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;

namespace Glance.Torrents.WinUI;

public sealed class TorrentComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent,
    IGlanceContentHandlingResultComponent,
    IGlanceIntent,
    IGlanceAttentionComponent,
    IAsyncDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken cancellationToken;
    private readonly TorrentAddCoordinator coordinator;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITorrentEngineService engine;
    private readonly TorrentExpandedView expandedView;
    private readonly Task initialization;
    private readonly ModuleResourceTextLocalizer<TorrentModule> localizer;
    private readonly ILogger<TorrentComponent> logger;
    private readonly GlanceModuleOptions<TorrentSettings> options;
    private readonly SemaphoreSlim promptSynchronization = new(1, 1);
    private readonly object disposalSynchronization = new();
    private readonly TorrentsViewModel viewModel;
    private Task? disposalTask;

    public TorrentComponent(ITorrentEngineService engine,
        TorrentAddCoordinator coordinator,
        TorrentsViewModel viewModel,
        GlanceModuleOptions<TorrentSettings> options,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<TorrentModule> localizer,
        ILogger<TorrentComponent> logger)
    {
        this.engine = engine;
        this.coordinator = coordinator;
        this.viewModel = viewModel;
        this.options = options;
        this.attentionService = attentionService;
        this.localizer = localizer;
        this.logger = logger;
        cancellationToken = cancellation.Token;

        TorrentCompactView compactView = new(viewModel);
        expandedView = new TorrentExpandedView(viewModel, localizer);
        dispatcherQueue = compactView.DispatcherQueue;
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        engine.SnapshotUpdated += HandleSnapshotUpdated;
        engine.TorrentCompleted += HandleTorrentCompleted;
        expandedView.PauseAllRequested += HandlePauseAllRequested;
        expandedView.ResumeAllRequested += HandleResumeAllRequested;
        expandedView.RemoveRequested += HandleRemoveRequested;

        initialization = engine.InitializeAsync(cancellationToken);
        options.Changed += HandleOptionsChanged;
    }

    public string Id => "Torrent";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Productivity;

    public string AccentResourceKey => "GlanceTorrentIconBrush";

    public string IconGlyph => "\uE896";

    public int Order => 38;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool IsAttentionEnabledByDefault => false;

    public GlanceIntentDescriptor Descriptor => new("Torrent.Add",
        Id,
        localizer.GetText("RouteDisplayName"),
        localizer.GetText("RouteDescription"),
        IconGlyph);

    public bool CanHandle(GlanceContentKind kind) => kind is GlanceContentKind.FilesAndFolders
        or GlanceContentKind.Text
        or GlanceContentKind.WebLink;

    public bool CanHandle(GlanceContentContext context) => TorrentInput.TryCreate(context, out _);

    public void BeginContentPreview(GlanceContentContext context)
    {
    }

    public void EndContentPreview()
    {
    }

    public Task HandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task<bool> IGlanceContentHandlingResultComponent.TryHandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task IGlanceIntent.InvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken) => HandleCoreAsync(context, cancellationToken);

    public ValueTask DisposeAsync()
    {
        lock (disposalSynchronization)
        {
            return new ValueTask(disposalTask ??= DisposeCoreAsync());
        }
    }

    private async Task<bool> HandleCoreAsync(GlanceContentContext context,
        CancellationToken externalCancellation = default)
    {
        if (!TorrentInput.TryCreate(context, out TorrentInput? input) || input is null)
        {
            return false;
        }

        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            externalCancellation);
        await initialization.WaitAsync(linkedCancellation.Token);
        await promptSynchronization.WaitAsync(linkedCancellation.Token);

        try
        {
            WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

            if (ownerWindowId is not WindowId windowId)
            {
                return false;
            }

            string downloadPath = ResolveDownloadPath(options.Current);
            Task<TorrentConfirmationResult?> confirmationTask = await RunOnDispatcherAsync(() => TorrentConfirmationWindow.ShowAsync(coordinator,
                input,
                downloadPath,
                localizer,
                windowId));
            TorrentConfirmationResult? result = await confirmationTask.WaitAsync(linkedCancellation.Token);

            if (result is null)
            {
                return false;
            }

            await coordinator.ConfirmAsync(result.Session,
                result.SelectedFiles,
                result.DownloadPath,
                linkedCancellation.Token);
            return true;
        }
        finally
        {
            _ = promptSynchronization.Release();
        }
    }

    private void HandleSnapshotUpdated(object? sender,
        TorrentSnapshotEventArgs args) => _ = dispatcherQueue.TryEnqueue(() =>
        {
            if (engine.ActiveTorrentIds.Contains(args.Snapshot.Id,
                StringComparer.OrdinalIgnoreCase))
            {
                viewModel.Update(args.Snapshot);
            }
        });

    private void HandleTorrentCompleted(object? sender,
        TorrentCompletedEventArgs args)
        => attentionService.RequestAttention(Id,
            GlanceAttentionLevel.Default,
            true);

    private void HandleOptionsChanged(object? sender,
        GlanceModuleOptionsChangedEventArgs<TorrentSettings> args) => _ = ApplySettingsAsync(args.Options);

    private async Task ApplySettingsAsync(TorrentSettings settings)
    {
        try
        {
            await initialization;
            await engine.ApplySettingsAsync(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply Torrent settings");
        }
    }

    private void HandlePauseAllRequested(object? sender,
        EventArgs args) => _ = UpdateTorrentStatesAsync(viewModel.GetPausableIds(),
        engine.PauseAsync,
        "pause");

    private void HandleResumeAllRequested(object? sender,
        EventArgs args) => _ = UpdateTorrentStatesAsync(viewModel.GetResumableIds(),
        engine.ResumeAsync,
        "resume");

    private async Task UpdateTorrentStatesAsync(IReadOnlyList<string> torrentIds,
        Func<string, Task> update,
        string operation)
    {
        foreach (string torrentId in torrentIds)
        {
            try
            {
                await update(torrentId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Failed to {Operation} torrent {TorrentId}",
                    operation,
                    torrentId);
            }
        }
    }

    private void HandleRemoveRequested(object? sender,
        string torrentId) => _ = RemoveAsync(torrentId);

    private async Task RemoveAsync(string torrentId)
    {
        try
        {
            WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

            if (ownerWindowId is not WindowId windowId)
            {
                return;
            }

            Task<TorrentRemovalChoice> removalTask = await RunOnDispatcherAsync(() => TorrentRemovalWindow.ShowAsync(localizer,
                windowId));
            TorrentRemovalChoice result = await removalTask.WaitAsync(cancellationToken);

            if (result == TorrentRemovalChoice.Cancel)
            {
                return;
            }

            await engine.RemoveAsync(torrentId,
                result == TorrentRemovalChoice.RemoveAndDeleteData);
            _ = await RunOnDispatcherAsync(() =>
            {
                viewModel.Remove(torrentId);
                return true;
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to remove torrent {TorrentId}",
                torrentId);
        }
    }

    private async Task DisposeCoreAsync()
    {
        engine.SnapshotUpdated -= HandleSnapshotUpdated;
        engine.TorrentCompleted -= HandleTorrentCompleted;
        options.Changed -= HandleOptionsChanged;
        expandedView.PauseAllRequested -= HandlePauseAllRequested;
        expandedView.ResumeAllRequested -= HandleResumeAllRequested;
        expandedView.RemoveRequested -= HandleRemoveRequested;
        cancellation.Cancel();

        try
        {
            await coordinator.DisposeAsync();
        }
        finally
        {
            promptSynchronization.Dispose();
            cancellation.Dispose();
        }
    }

    private Task<T> RunOnDispatcherAsync<T>(Func<T> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(action());
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    completion.TrySetResult(action());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The Torrent UI is unavailable."));
        }

        return completion.Task;
    }

    internal static string ResolveDownloadPath(TorrentSettings settings) => string.IsNullOrWhiteSpace(settings.DefaultDownloadPath)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "Glance Torrents")
        : settings.DefaultDownloadPath;
}
