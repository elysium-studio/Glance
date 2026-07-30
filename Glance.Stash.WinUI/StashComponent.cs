using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Stash.WinUI;

public sealed partial class StashComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent,
    IGlanceIntent
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly StashTextCopyService copyService;
    private readonly StashRepository repository;
    private readonly StashTextEditorService textEditorService;
    private readonly StashViewModel viewModel;

    public StashComponent(StashViewModel viewModel,
        StashTextCopyService copyService,
        StashTextEditorService textEditorService,
        StashRepository repository,
        ModuleResourceTextLocalizer<StashModule> localizer)
    {
        this.viewModel = viewModel;
        this.copyService = copyService;
        this.textEditorService = textEditorService;
        this.repository = repository;
        this.localizer = localizer;

        StashCompactView compactView = new(viewModel);
        StashExpandedView expandedView = new(viewModel);
        dispatcherQueue = compactView.DispatcherQueue;

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ConfigureActions(CopyAsync, OpenAsync, OpenInEditorAsync, RemoveAsync);
        viewModel.Restore(repository.Load());
    }

    public string Id => "Stash";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 55;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public GlanceIntentDescriptor Descriptor => new("Stash.Share",
        Id,
        localizer.GetText("ShareIntentDisplayName"),
        localizer.GetText("ShareIntentDescription"),
        "\uE718");

    public bool CanHandle(GlanceContentKind kind) =>
        kind is GlanceContentKind.Text or GlanceContentKind.WebLink;

    public async Task HandleAsync(GlanceContentContext context)
    {
        if (!CanHandle(context.Kind) ||
            string.IsNullOrWhiteSpace(context.Content))
        {
            return;
        }

        StashItem? item = await AddAsync(context.Content, context.Kind == GlanceContentKind.WebLink);

        if (item is not null)
        {
            repository.Save(item.ToEntry());
        }
    }

    Task IGlanceIntent.InvokeAsync(GlanceContentContext context,
        CancellationToken cancellationToken) =>
        HandleAsync(context);

    private Task CopyAsync(StashItem item) =>
        copyService.CopyAsync(item.Content);

    private Task OpenAsync(StashItem item)
    {
        if (!item.CanOpen)
        {
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo(item.Content)
            {
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
        }

        return Task.CompletedTask;
    }

    private async Task OpenInEditorAsync(StashItem item)
    {
        if (!item.CanOpenInEditor)
        {
            return;
        }

        try
        {
            await textEditorService.OpenAsync(item.Id, item.Content, content => UpdateContentAsync(item.Id, content));
        }
        catch (Exception)
        {
        }
    }

    private Task RemoveAsync(StashItem item)
    {
        textEditorService.Remove(item.Id);
        repository.Remove(item.Id);
        return Task.CompletedTask;
    }

    private Task UpdateContentAsync(string id,
        string content)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            SaveUpdatedContent(id, content);
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            SaveUpdatedContent(id, content);
            completion.TrySetResult();
        }))
        {
            completion.TrySetException(new InvalidOperationException("The Stash UI dispatcher is unavailable."));
        }

        return completion.Task;
    }

    private void SaveUpdatedContent(string id,
        string content)
    {
        StashItem? item = viewModel.UpdateContent(id, content);

        if (item is not null)
        {
            repository.Save(item.ToEntry());
        }
    }

    private Task<StashItem?> AddAsync(string content,
        bool isLink)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(viewModel.Add(content, isLink));
        }

        TaskCompletionSource<StashItem?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            completion.TrySetResult(viewModel.Add(content, isLink));
        }))
        {
            completion.TrySetException(new InvalidOperationException("The Stash UI dispatcher is unavailable."));
        }

        return completion.Task;
    }
}
