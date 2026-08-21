using Glance.Application.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using System.Threading.Channels;

namespace Glance.Archive.WinUI;

public sealed class ArchiveComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent,
    IGlanceContentHandlingResultComponent,
    IGlanceIntent,
    IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly object disposalSynchronization = new();
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ArchiveExpandedView expandedView;
    private readonly Channel<ArchiveJob> jobs = Channel.CreateUnbounded<ArchiveJob>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly IArchiveService archiveService;
    private readonly ModuleResourceTextLocalizer<ArchiveModule> localizer;
    private readonly object queueSynchronization = new();
    private readonly List<CancellationTokenSource> retiredJobCancellations = [];
    private readonly SemaphoreSlim promptSynchronization = new(1, 1);
    private readonly ArchiveViewModel viewModel;
    private readonly Task worker;
    private Task? disposalTask;
    private CancellationTokenSource jobCancellation = new();
    private long jobGeneration;
    private int pendingJobs;

    public ArchiveComponent(ArchiveViewModel viewModel, IArchiveService archiveService, ModuleResourceTextLocalizer<ArchiveModule> localizer)
    {
        this.viewModel = viewModel;
        this.archiveService = archiveService;
        this.localizer = localizer;

        ArchiveCompactView compactView = new(viewModel);
        expandedView = new ArchiveExpandedView(viewModel, localizer, StopJobs);
        dispatcherQueue = compactView.DispatcherQueue;

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        worker = ProcessQueueAsync(cancellation.Token);
    }

    public string Id => "Archive";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Productivity;

    public string AccentResourceKey => "GlanceArchiveIconBrush";

    public int Order => 62;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public GlanceIntentDescriptor Descriptor => new("Archive.Files", Id, localizer.GetText("RouteDisplayName"), localizer.GetText("RouteDescription"), "\uE7B8");

    public bool CanHandle(GlanceContentKind kind) => kind == GlanceContentKind.FilesAndFolders;

    public bool CanHandle(GlanceContentContext context) => CanHandle(context.Kind) && context.StorageItems.Count > 0;

    public void BeginContentPreview(GlanceContentContext context) => viewModel.Preview(ContainsOnlyArchives(context), context.StorageItems.Count);

    public void EndContentPreview() => viewModel.CancelPreview();

    public Task HandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task<bool> IGlanceContentHandlingResultComponent.TryHandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task<bool> IGlanceIntent.TryInvokeAsync(GlanceContentContext context, CancellationToken cancellationToken) => HandleCoreAsync(context);

    Task IGlanceIntent.InvokeAsync(GlanceContentContext context, CancellationToken cancellationToken) => HandleAsync(context);

    public ValueTask DisposeAsync()
    {
        lock (disposalSynchronization)
        {
            return new ValueTask(disposalTask ??= DisposeCoreAsync());
        }
    }

    private async Task<bool> HandleCoreAsync(GlanceContentContext context)
    {
        if (!CanHandle(context))
        {
            return false;
        }

        await promptSynchronization.WaitAsync(cancellation.Token);

        try
        {
            WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

            if (ownerWindowId is not WindowId windowId)
            {
                return false;
            }

            Task<ArchiveSelection?> editorTask = await RunOnDispatcherAsync(() => ArchiveEditorWindow.ShowAsync(context, localizer, windowId));
            ArchiveSelection? selection = await editorTask;

            if (selection is null)
            {
                _ = await RunOnDispatcherAsync(() =>
                {
                    viewModel.CancelPreview();
                    return true;
                });
                return false;
            }

            int queued;
            long generation;
            CancellationToken jobCancellationToken;

            lock (queueSynchronization)
            {
                queued = ++pendingJobs;
                generation = jobGeneration;
                jobCancellationToken = jobCancellation.Token;
            }

            await jobs.Writer.WriteAsync(new ArchiveJob(context, selection.Options, generation, jobCancellationToken), cancellation.Token);
            return queued > 0;
        }
        finally
        {
            _ = promptSynchronization.Release();
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (ArchiveJob job in jobs.Reader.ReadAllAsync(cancellationToken))
        {
            if (job.CancellationToken.IsCancellationRequested)
            {
                continue;
            }

            _ = await RunOnDispatcherAsync(() =>
            {
                viewModel.Begin(job.Options.Operation, job.Content.StorageItems.Count);
                return true;
            });
            int completed = 0;
            string? failureReason = null;

            try
            {
                using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, job.CancellationToken);
                ArchiveItem[] items = [.. job.Content.StorageItems.Select(item => new ArchiveItem(item.Path, item.Name, item.IsFolder))];
                IProgress<ArchiveOperationProgress>? progress = null;

                if (job.Options.Operation == ArchiveOperation.Create)
                {
                    _ = await archiveService.CreateAsync(items, job.Options, progress, linkedCancellation.Token);
                    completed = 1;
                }
                else
                {
                    foreach (ArchiveItem item in items)
                    {
                        _ = job.Options.Operation == ArchiveOperation.Extract ? await archiveService.ExtractAsync(item.Path, progress, linkedCancellation.Token) : await archiveService.ConvertAsync(item.Path, job.Options, progress, linkedCancellation.Token);
                        completed++;
                    }
                }
            }
            catch (OperationCanceledException) when (job.CancellationToken.IsCancellationRequested)
            {
                continue;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failureReason = CreateErrorMessage(exception);
            }

            int remaining;

            lock (queueSynchronization)
            {
                if (job.Generation != jobGeneration)
                {
                    continue;
                }

                remaining = --pendingJobs;
            }

            _ = await RunOnDispatcherAsync(() =>
            {
                if (failureReason is not null)
                {
                    viewModel.Fail(failureReason);
                }
                else if (remaining == 0)
                {
                    viewModel.Complete(job.Options.Operation, completed);
                }

                return true;
            });
        }
    }

    private void StopJobs()
    {
        CancellationTokenSource cancellationToStop;

        lock (queueSynchronization)
        {
            if (pendingJobs == 0)
            {
                return;
            }

            cancellationToStop = jobCancellation;
            retiredJobCancellations.Add(cancellationToStop);
            jobCancellation = new CancellationTokenSource();
            jobGeneration++;
            pendingJobs = 0;
        }

        cancellationToStop.Cancel();
        viewModel.Reset();
    }

    private async Task DisposeCoreAsync()
    {
        _ = jobs.Writer.TryComplete();
        cancellation.Cancel();

        lock (queueSynchronization)
        {
            jobCancellation.Cancel();

            foreach (CancellationTokenSource retired in retiredJobCancellations)
            {
                retired.Cancel();
            }
        }

        try
        {
            await worker;
        }
        catch (OperationCanceledException)
        {
        }

        cancellation.Dispose();
        jobCancellation.Dispose();

        foreach (CancellationTokenSource retired in retiredJobCancellations)
        {
            retired.Dispose();
        }

        promptSynchronization.Dispose();
    }

    private string CreateErrorMessage(Exception exception) => exception is UnauthorizedAccessException ? localizer.GetText("ArchiveAccessDenied") : exception is InvalidDataException ? localizer.GetText("ArchiveInvalid") : exception is IOException ? localizer.GetText("ArchiveFileUnavailable") : localizer.GetText("ArchiveOperationFailedDetail");

    private static bool ContainsOnlyArchives(GlanceContentContext context) => context.StorageItems.All(item => !item.IsFolder && ArchiveFile.IsArchive(item.Path));

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
                _ = completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetException(new InvalidOperationException("The Archive UI dispatcher is unavailable."));
        }

        return completion.Task;
    }
}
