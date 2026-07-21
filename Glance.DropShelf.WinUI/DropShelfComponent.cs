using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Glance.DropShelf.WinUI;

public sealed class DropShelfComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly DropShelfTransferStore transferStore;
    private readonly DropShelfViewModel viewModel;

    public DropShelfComponent(
        DropShelfViewModel viewModel,
        DropShelfTransferStore transferStore,
        ModuleResourceTextLocalizer<DropShelfModule> localizer)
    {
        this.viewModel = viewModel;
        this.transferStore = transferStore;
        this.localizer = localizer;

        DropShelfCompactView compactView = new(viewModel);
        DropShelfExpandedView expandedView = new(viewModel, transferStore);
        dispatcherQueue = compactView.DispatcherQueue;

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;
    }

    public string Id => "DropShelf";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 60;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public bool CanHandle(GlanceContentKind kind) =>
        kind == GlanceContentKind.FilesAndFolders;

    public async Task HandleAsync(GlanceContentContext context)
    {
        if (!CanHandle(context.Kind))
        {
            return;
        }

        DropShelfItem[] items = context.StorageItems
            .Select(item => new DropShelfItem(
                item.Path,
                item.Name,
                item.IsFolder))
            .ToArray();

        IReadOnlyList<DropShelfItem> stagedItems =
            await transferStore.StageAsync(items);
        await AddItemsAsync(stagedItems);
    }

    private Task AddItemsAsync(IReadOnlyList<DropShelfItem> items)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            viewModel.AddItems(items);
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                viewModel.AddItems(items);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException(
                "The Drop Shelf UI dispatcher is unavailable."));
        }

        return completion.Task;
    }
}
