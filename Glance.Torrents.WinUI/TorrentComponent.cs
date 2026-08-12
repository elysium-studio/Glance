using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Torrents.WinUI;

public sealed class TorrentComponent : IGlanceComponent, IGlanceConnectedAnimationComponent, IGlanceContextAwareComponent, IGlanceContentHandlingResultComponent, IGlanceIntent, IGlanceAttentionComponent, IAsyncDisposable
{
    private readonly ITorrentEngineService engine;
    private readonly TorrentAddCoordinator coordinator;
    private readonly TorrentsViewModel viewModel;
    private readonly TorrentExpandedView expandedView;
    private readonly DispatcherQueue dispatcher;
    private readonly GlanceModuleOptions<TorrentSettings> options;
    private readonly IGlanceAttentionService attention;
    private readonly ModuleResourceTextLocalizer<TorrentModule> localizer;
    private readonly SemaphoreSlim promptLock = new(1, 1);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task initialization;
    private int disposed;

    public TorrentComponent(ITorrentEngineService engine, TorrentAddCoordinator coordinator, TorrentsViewModel viewModel,
        GlanceModuleOptions<TorrentSettings> options, IGlanceAttentionService attention, ModuleResourceTextLocalizer<TorrentModule> localizer)
    {
        this.engine = engine;
        this.coordinator = coordinator;
        this.viewModel = viewModel;
        this.options = options;
        this.attention = attention;
        this.localizer = localizer;
        TorrentCompactView compact = new(viewModel);
        expandedView = new TorrentExpandedView(viewModel, localizer);
        dispatcher = compact.DispatcherQueue;
        CompactContent = compact;
        ExpandedContent = expandedView;
        CompactAnimationElement = compact.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        engine.SnapshotUpdated += HandleSnapshotUpdated;
        engine.TorrentCompleted += HandleTorrentCompleted;
        options.Changed += HandleOptionsChanged;
        expandedView.PauseAllRequested += HandlePauseAllRequested;
        expandedView.ResumeAllRequested += HandleResumeAllRequested;
        expandedView.RemoveRequested += HandleRemoveRequested;
        initialization = engine.InitializeAsync(cancellation.Token);
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
    public GlanceIntentDescriptor Descriptor => new("Torrent.Add", Id, localizer.GetText("RouteDisplayName"), localizer.GetText("RouteDescription"), IconGlyph, MatchPriority: 100);

    public bool CanHandle(GlanceContentKind kind) => kind is GlanceContentKind.FilesAndFolders or GlanceContentKind.Text or GlanceContentKind.WebLink;
    public bool CanHandle(GlanceContentContext context) => TorrentInput.TryCreate(context, out _);
    public void BeginContentPreview(GlanceContentContext context) { }
    public void EndContentPreview() { }
    public Task HandleAsync(GlanceContentContext context) => HandleCoreAsync(context);
    Task<bool> IGlanceContentHandlingResultComponent.TryHandleAsync(GlanceContentContext context) => HandleCoreAsync(context);
    Task IGlanceIntent.InvokeAsync(GlanceContentContext context, CancellationToken cancellationToken) => HandleCoreAsync(context, cancellationToken);

    private async Task<bool> HandleCoreAsync(GlanceContentContext context, CancellationToken externalCancellation = default)
    {
        if (!TorrentInput.TryCreate(context, out TorrentInput? input) || input is null) return false;
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, externalCancellation);
        await initialization.WaitAsync(linked.Token);
        await promptLock.WaitAsync(linked.Token);
        try
        {
            WindowId? owner = await OnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);
            if (owner is not WindowId windowId) return false;
            string path = ResolveDownloadPath(options.Current);
            Task<TorrentConfirmationResult?> overlay = await OnDispatcherAsync(() => TorrentConfirmationWindow.ShowAsync(coordinator, input, path, localizer, windowId));
            TorrentConfirmationResult? result = await overlay.WaitAsync(linked.Token);
            if (result is null) return false;
            await coordinator.ConfirmAsync(result.Session, result.SelectedFiles, linked.Token);
            return true;
        }
        finally { _ = promptLock.Release(); }
    }

    private void HandleSnapshotUpdated(object? sender, TorrentSnapshotEventArgs args) => _ = dispatcher.TryEnqueue(() => viewModel.Update(args.Snapshot));
    private void HandleTorrentCompleted(object? sender, TorrentCompletedEventArgs args)
    {
        if (options.Current.RequestAttentionOnCompletion) attention.RequestAttention(Id, GlanceAttentionLevel.Default, true);
    }
    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<TorrentSettings> args) => _ = ApplySettingsAsync(args.Options);
    private async Task ApplySettingsAsync(TorrentSettings settings)
    {
        try { await initialization; await engine.ApplySettingsAsync(settings, cancellation.Token); } catch (OperationCanceledException) { }
    }
    private async void HandlePauseAllRequested(object? sender, EventArgs args)
    {
        foreach (string id in viewModel.GetPausableIds())
        {
            try { await engine.PauseAsync(id); } catch { }
        }
    }
    private async void HandleResumeAllRequested(object? sender, EventArgs args)
    {
        foreach (string id in viewModel.GetResumableIds())
        {
            try { await engine.ResumeAsync(id); } catch { }
        }
    }
    private async void HandleRemoveRequested(object? sender, string id)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = expandedView.XamlRoot,
            Title = localizer.GetText("RemoveTitle"),
            Content = localizer.GetText("RemoveDescription"),
            PrimaryButtonText = localizer.GetText("RemoveListOnly"),
            SecondaryButtonText = localizer.GetText("RemoveAndDelete"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) return;
        await engine.RemoveAsync(id, result == ContentDialogResult.Secondary);
        viewModel.Remove(id);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        cancellation.Cancel();
        engine.SnapshotUpdated -= HandleSnapshotUpdated;
        engine.TorrentCompleted -= HandleTorrentCompleted;
        options.Changed -= HandleOptionsChanged;
        expandedView.PauseAllRequested -= HandlePauseAllRequested;
        expandedView.ResumeAllRequested -= HandleResumeAllRequested;
        expandedView.RemoveRequested -= HandleRemoveRequested;
        await coordinator.DisposeAsync();
        promptLock.Dispose();
        cancellation.Dispose();
    }

    private Task<T> OnDispatcherAsync<T>(Func<T> action)
    {
        if (dispatcher.HasThreadAccess) return Task.FromResult(action());
        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() => { try { completion.TrySetResult(action()); } catch (Exception exception) { completion.TrySetException(exception); } }))
            completion.TrySetException(new InvalidOperationException("The Torrent UI is unavailable."));
        return completion.Task;
    }

    internal static string ResolveDownloadPath(TorrentSettings settings) => string.IsNullOrWhiteSpace(settings.DefaultDownloadPath)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Glance Torrents")
        : settings.DefaultDownloadPath;
}
