using Glance.Application.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using System.Threading.Channels;

namespace Glance.QuickConvert.WinUI;

public sealed partial class QuickConvertComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent,
    IGlanceContentHandlingResultComponent,
    IGlanceIntent,
    IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly Channel<QuickConversionJob> jobs = Channel.CreateUnbounded<QuickConversionJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly object disposalSynchronization = new();
    private readonly object queueSynchronization = new();
    private readonly List<CancellationTokenSource> retiredJobCancellations = [];
    private readonly SemaphoreSlim promptSynchronization = new(1, 1);
    private readonly DispatcherQueue dispatcherQueue;
    private readonly QuickConvertExpandedView expandedView;
    private readonly IGlanceQuickConverterRegistry registry;
    private readonly ModuleResourceTextLocalizer<QuickConvertModule> localizer;
    private readonly QuickConvertViewModel viewModel;
    private readonly Task worker;
    private Task? disposalTask;
    private int failedConversions;
    private int pendingJobs;
    private int successfulConversions;
    private CancellationTokenSource jobCancellation = new();
    private long jobGeneration;

    public QuickConvertComponent(QuickConvertViewModel viewModel,
        IGlanceQuickConverterRegistry registry,
        ModuleResourceTextLocalizer<QuickConvertModule> localizer)
    {
        this.viewModel = viewModel;
        this.registry = registry;
        this.localizer = localizer;
        QuickConvertCompactView compactView = new(viewModel);
        expandedView = new QuickConvertExpandedView(viewModel, localizer, StopJobs);
        dispatcherQueue = compactView.DispatcherQueue;
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
        worker = ProcessQueueAsync(cancellation.Token);
    }

    public string Id => "QuickConvert";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.MediaAndCapture;

    public string AccentResourceKey => "GlanceQuickConvertIconBrush";

    public int Order => 72;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public GlanceIntentDescriptor Descriptor => new("QuickConvert.Convert",
        Id,
        localizer.GetText("RouteDisplayName"),
        localizer.GetText("RouteDescription"),
        "\uE8B1");

    public bool CanHandle(GlanceContentKind kind) => kind == GlanceContentKind.FilesAndFolders;

    public bool CanHandle(GlanceContentContext context) => context.Kind == GlanceContentKind.FilesAndFolders && registry.GetConverters(context.StorageItems).Count > 0;

    public void BeginContentPreview(GlanceContentContext context) => viewModel.Prepare(context.StorageItems.Count);

    public void EndContentPreview() => viewModel.CancelPreview();

    public Task HandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task<bool> IGlanceContentHandlingResultComponent.TryHandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task<bool> IGlanceIntent.TryInvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken) => HandleCoreAsync(context);

    private async Task<bool> HandleCoreAsync(GlanceContentContext context)
    {
        await promptSynchronization.WaitAsync(cancellation.Token);

        try
        {
            IReadOnlyList<IGlanceQuickConverter> converters = registry.GetConverters(context.StorageItems);

            if (converters.Count == 0)
            {
                return false;
            }

            WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

            if (ownerWindowId is not WindowId windowId)
            {
                return false;
            }

            Task<QuickConversionSelection?> editorTask = await RunOnDispatcherAsync(() => QuickConvertEditorWindow.ShowAsync(converters, context.StorageItems, localizer, windowId));
            QuickConversionSelection? selection = await editorTask;

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
                if (pendingJobs == 0)
                {
                    successfulConversions = 0;
                    failedConversions = 0;
                }

                queued = ++pendingJobs;
                generation = jobGeneration;
                jobCancellationToken = jobCancellation.Token;
            }

            _ = await RunOnDispatcherAsync(() =>
            {
                viewModel.Enqueue(queued);
                return true;
            });
            await jobs.Writer.WriteAsync(new QuickConversionJob(selection.Converter,
                context.StorageItems,
                selection.Options,
                generation,
                jobCancellationToken), cancellation.Token);
            return true;
        }
        finally
        {
            _ = promptSynchronization.Release();
        }
    }

    Task IGlanceIntent.InvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken) => HandleAsync(context);

    public ValueTask DisposeAsync()
    {
        lock (disposalSynchronization)
        {
            return new ValueTask(disposalTask ??= DisposeCoreAsync());
        }
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

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (QuickConversionJob job in jobs.Reader.ReadAllAsync(cancellationToken))
        {
            if (job.CancellationToken.IsCancellationRequested)
            {
                continue;
            }

            _ = await RunOnDispatcherAsync(() =>
            {
                viewModel.BeginConversion(job.Items.Count);
                return true;
            });
            int successful;
            int failed;

            try
            {
                using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, job.CancellationToken);
                IReadOnlyList<GlanceQuickConversionResult> results = await job.Converter.ConvertAsync(new GlanceQuickConversionRequest(job.Items, job.Options), linkedCancellation.Token);
                successful = results.Count(result => result.IsSuccessful);
                failed = Math.Max(job.Items.Count - successful, results.Count - successful);
            }
            catch (OperationCanceledException) when (job.CancellationToken.IsCancellationRequested)
            {
                continue;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                successful = 0;
                failed = job.Items.Count;
            }
            int remaining;
            int totalSuccessful;
            int totalFailed;

            lock (queueSynchronization)
            {
                if (job.Generation != jobGeneration)
                {
                    continue;
                }

                successfulConversions += successful;
                failedConversions += failed;
                remaining = --pendingJobs;
                totalSuccessful = successfulConversions;
                totalFailed = failedConversions;
            }

            _ = await RunOnDispatcherAsync(() =>
            {
                viewModel.Complete(totalSuccessful, totalFailed, remaining);
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
            successfulConversions = 0;
            failedConversions = 0;
        }

        cancellationToStop.Cancel();
        viewModel.StopConversions();
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
                _ = completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        }))
        {
            _ = completion.TrySetException(new InvalidOperationException("The Quick Convert UI dispatcher is unavailable."));
        }

        return completion.Task;
    }
}
