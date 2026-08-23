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
    private string? conversionFailureReason;
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

    public string SettingsCategory => GlanceModuleCategories.Productivity;

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

    public bool CanHandle(GlanceContentKind kind) => kind is GlanceContentKind.FilesAndFolders or GlanceContentKind.WebLink or GlanceContentKind.Text;

    public bool CanHandle(GlanceContentContext context) => registry.GetConverters(context).Count > 0;

    public void BeginContentPreview(GlanceContentContext context) => viewModel.Prepare(context.StorageItems.Count == 0 ? 1 : context.StorageItems.Count);

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
            IReadOnlyList<IGlanceQuickConverter> converters = registry.GetConverters(context);

            if (converters.Count == 0)
            {
                return false;
            }

            WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

            if (ownerWindowId is not WindowId windowId)
            {
                return false;
            }

            Task<QuickConversionSelection?> editorTask = await RunOnDispatcherAsync(() => QuickConvertEditorWindow.ShowAsync(converters, context, localizer, windowId));
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
                    conversionFailureReason = null;
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
                context,
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
                viewModel.BeginConversion(job.Content.StorageItems.Count == 0 ? 1 : job.Content.StorageItems.Count);
                return true;
            });
            int successful;
            int failed;
            string? failureReason = null;
            bool setupFailed = false;

            try
            {
                using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, job.CancellationToken);
                Progress<GlanceQuickConversionProgress> progress = new(HandleConversionProgress);
                IReadOnlyList<GlanceQuickConversionResult> results = await job.Converter.ConvertAsync(new GlanceQuickConversionRequest(job.Content,
                    job.Options,
                    progress), linkedCancellation.Token);
                successful = results.Count(result => result.IsSuccessful);
                int expected = job.Content.StorageItems.Count == 0 ? 1 : job.Content.StorageItems.Count;
                failed = Math.Max(expected - successful, results.Count - successful);
                failureReason = results.Where(result => !result.IsSuccessful && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                    .Select(CreateFailureReason)
                    .FirstOrDefault();
            }
            catch (OperationCanceledException) when (job.CancellationToken.IsCancellationRequested)
            {
                continue;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (GlanceQuickConverterSetupException)
            {
                successful = 0;
                failed = job.Content.StorageItems.Count == 0 ? 1 : job.Content.StorageItems.Count;
                setupFailed = true;
            }
            catch (Exception exception)
            {
                successful = 0;
                failed = job.Content.StorageItems.Count == 0 ? 1 : job.Content.StorageItems.Count;
                failureReason = exception.Message;
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
                conversionFailureReason ??= failureReason;
                remaining = --pendingJobs;
                totalSuccessful = successfulConversions;
                totalFailed = failedConversions;
                failureReason = conversionFailureReason;
            }

            _ = await RunOnDispatcherAsync(() =>
            {
                if (setupFailed && remaining == 0)
                {
                    viewModel.ShowToolSetupFailure();
                }
                else
                {
                    viewModel.Complete(totalSuccessful, totalFailed, remaining, failureReason);
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
            successfulConversions = 0;
            failedConversions = 0;
            conversionFailureReason = null;
        }

        cancellationToStop.Cancel();
        viewModel.StopConversions();
    }

    private static string CreateFailureReason(GlanceQuickConversionResult result)
    {
        if (Uri.TryCreate(result.SourcePath, UriKind.Absolute, out Uri? source) && source.Scheme is "http" or "https")
        {
            return result.ErrorMessage!;
        }

        string sourceName = Path.GetFileName(result.SourcePath);
        return string.IsNullOrWhiteSpace(sourceName)
            ? result.ErrorMessage!
            : $"{sourceName}: {result.ErrorMessage}";
    }

    private void HandleConversionProgress(GlanceQuickConversionProgress progress) => _ = RunOnDispatcherAsync(() =>
    {
        if (progress.Stage == GlanceQuickConversionStage.Setup)
        {
            if (progress.IsComplete)
            {
                viewModel.CompleteToolSetup();
            }
            else
            {
                viewModel.ShowToolSetup(progress.Progress);
            }
        }

        return true;
    });

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
