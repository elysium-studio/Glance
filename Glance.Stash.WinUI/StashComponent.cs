using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Stash.WinUI;

public sealed partial class StashComponent :
    IGlanceComponent,
    IGlanceActionProvider,
    IGlanceActionValidator,
    IGlanceConnectedAnimationComponent,
    IGlanceContextAwareComponent,
    IGlanceIntent
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly StashTextCopyService copyService;
    private readonly StashRepository repository;
    private readonly StashTextViewerService textViewerService;
    private readonly StashViewModel viewModel;

    public StashComponent(StashViewModel viewModel,
        StashTextCopyService copyService,
        StashTextViewerService textViewerService,
        StashRepository repository,
        ModuleResourceTextLocalizer<StashModule> localizer)
    {
        this.viewModel = viewModel;
        this.copyService = copyService;
        this.textViewerService = textViewerService;
        this.repository = repository;
        this.localizer = localizer;

        StashCompactView compactView = new(viewModel);
        StashExpandedView expandedView = new(viewModel);
        dispatcherQueue = compactView.DispatcherQueue;

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        viewModel.ConfigureActions(CopyAsync, OpenAsync, ViewFullTextAsync, RemoveAsync);
        viewModel.Restore(repository.Load());
    }

    public string Id => "Stash";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public string SettingsCategory => GlanceModuleCategories.Productivity;

    public int Order => 55;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public IReadOnlyList<GlanceActionDescriptor> GetActions() =>
    [
        new GlanceActionDescriptor("Stash.Add",
            Id,
            "Add to Stash",
            "Save text or a web link in Stash.",
            [
                new GlanceActionParameterDescriptor("content", GlanceActionParameterType.String, "The text or web link to save."),
                new GlanceActionParameterDescriptor("isLink", GlanceActionParameterType.Boolean, "Whether the content should be treated as a web link.", IsRequired: false)
            ],
            Presentation: GlanceActionPresentation.Expanded)
        {
            SemanticTags = ["stash", "save", "keep", "remember", "store", "text", "note", "link", "url", "website"],
            ExampleUtterances = ["stash this text", "save this link in my stash", "remember this for later"]
        }
    ];

    public Task<GlanceActionResult?> ValidateAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ActionId, "Stash.Add", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<GlanceActionResult?>(null);
        }

        string? content = request.GetString("content");

        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("What should I add to Stash?", "Say the text or link you want to keep."));
        }

        if (request.GetBoolean("isLink") == true &&
            (!Uri.TryCreate(content, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https")))
        {
            return Task.FromResult<GlanceActionResult?>(GlanceActionResult.InvalidArguments("That doesn't look like a complete link.", "Try saying or sharing the link again."));
        }

        return Task.FromResult<GlanceActionResult?>(null);
    }

    public async Task<GlanceActionResult> InvokeAsync(GlanceActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ActionId != "Stash.Add" || request.GetString("content") is not string content)
        {
            return GlanceActionResult.InvalidArguments();
        }

        StashItem? item = await AddAsync(content, request.GetBoolean("isLink") == true);

        if (item is null)
        {
            return GlanceActionResult.InvalidArguments("The content is empty.");
        }

        repository.Save(item.ToEntry());
        return GlanceActionResult.Success();
    }

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

    private async Task ViewFullTextAsync(StashItem item)
    {
        if (!item.CanViewFullText)
        {
            return;
        }

        try
        {
            await textViewerService.OpenAsync(item.Id, item.Content);
        }
        catch (Exception)
        {
        }
    }

    private Task RemoveAsync(StashItem item)
    {
        textViewerService.Remove(item.Id);
        repository.Remove(item.Id);
        return Task.CompletedTask;
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
