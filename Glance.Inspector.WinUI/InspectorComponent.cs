using Glance.Application.Abstractions;
using Glance.Inspector;
using Microsoft.UI;
using Microsoft.UI.Dispatching;

namespace Glance.Inspector.WinUI;

public sealed class InspectorComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent,
    IGlanceContentHandlingResultComponent,
    IGlanceIntent,
    IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly DispatcherQueue dispatcherQueue;
    private readonly InspectorExpandedView expandedView;
    private readonly ModuleResourceTextLocalizer<InspectorModule> localizer;
    private readonly IGlanceInspectorProviderRegistry registry;
    private readonly SemaphoreSlim synchronization = new(1, 1);
    private readonly InspectorViewModel viewModel;
    private int disposed;

    public InspectorComponent(InspectorViewModel viewModel, IGlanceInspectorProviderRegistry registry, ModuleResourceTextLocalizer<InspectorModule> localizer)
    {
        this.viewModel = viewModel;
        this.registry = registry;
        this.localizer = localizer;
        InspectorCompactView compactView = new(viewModel);
        expandedView = new InspectorExpandedView(viewModel);
        dispatcherQueue = compactView.DispatcherQueue;
        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
    }

    public string Id => "Inspector";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Productivity;

    public string AccentResourceKey => "GlanceInspectorIconBrush";

    public int Order => 74;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public GlanceIntentDescriptor Descriptor => new("Inspector.Inspect", Id, localizer.GetText("RouteDisplayName"), localizer.GetText("RouteDescription"), "\uE721");

    public bool CanHandle(GlanceContentKind kind) => kind == GlanceContentKind.FilesAndFolders;

    public bool CanHandle(GlanceContentContext context) => registry.GetProviders(context).Count > 0;

    public void BeginContentPreview(GlanceContentContext context) => viewModel.Preview(GetSubject(context));

    public void EndContentPreview()
    {
        if (!viewModel.IsBusy)
        {
            viewModel.Cancel();
        }
    }

    public Task HandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task<bool> IGlanceContentHandlingResultComponent.TryHandleAsync(GlanceContentContext context) => HandleCoreAsync(context);

    Task IGlanceIntent.InvokeAsync(GlanceContentContext context, CancellationToken cancellationToken) => HandleCoreAsync(context);

    Task<bool> IGlanceIntent.TryInvokeAsync(GlanceContentContext context, CancellationToken cancellationToken) => HandleCoreAsync(context);

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            synchronization.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<bool> HandleCoreAsync(GlanceContentContext context)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return false;
        }

        await synchronization.WaitAsync(cancellation.Token);

        try
        {
            IReadOnlyList<IGlanceInspectorProvider> providers = registry.GetProviders(context);

            if (providers.Count == 0)
            {
                return false;
            }

            string subject = GetSubject(context);
            await RunOnDispatcherAsync(() => viewModel.Begin(subject));
            GlanceInspectionResult[] results = await Task.WhenAll(providers.Select(provider => InspectAsync(provider, context, cancellation.Token)));
            GlanceInspectionSection[] sections = [.. results.SelectMany(result => result.Sections)];
            IGlanceInspectionAction[] actions = [.. results.SelectMany(result => result.Actions).GroupBy(action => action.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First())];
            int propertyCount = sections.Sum(section => section.Properties.Count);
            await RunOnDispatcherAsync(() => viewModel.Complete(subject, propertyCount, providers.Count));
            WindowId? ownerWindowId = await RunOnDispatcherAsync(() => expandedView.XamlRoot?.ContentIslandEnvironment.AppWindowId);

            if (ownerWindowId is WindowId windowId)
            {
                Task overlay = await RunOnDispatcherAsync(() => InspectorOverlayWindow.ShowAsync(sections, actions, localizer, windowId));
                await overlay;
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            await RunOnDispatcherAsync(() => viewModel.Fail(GetSubject(context)));
            return false;
        }
        finally
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                _ = synchronization.Release();
            }
        }
    }

    private async Task<GlanceInspectionResult> InspectAsync(IGlanceInspectorProvider provider, GlanceContentContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.InspectAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new GlanceInspectionResult([new GlanceInspectionSection(provider.Descriptor.DisplayName, [new GlanceInspectionProperty(localizer.GetText("Status"), localizer.GetText("ProviderFailed"))])], []);
        }
    }

    private string GetSubject(GlanceContentContext context) => context.StorageItems.Count switch
    {
        0 => localizer.GetText("NoSelection"),
        1 => context.StorageItems[0].Name,
        _ => string.Format(localizer.GetText("MultipleItems"), context.StorageItems.Count)
    };

    private Task RunOnDispatcherAsync(Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("The Inspector UI dispatcher is unavailable."));
        }

        return completion.Task;
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
            completion.TrySetException(new InvalidOperationException("The Inspector UI dispatcher is unavailable."));
        }

        return completion.Task;
    }
}
