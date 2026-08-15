using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Glance.Shell;

public sealed class GlanceAssistantSemanticResolverService :
    ObservableObject,
    IGlanceAssistantSemanticResolverService
{
    private readonly IGlanceActionService actionService;
    private readonly ILogger<GlanceAssistantSemanticResolverService> logger;
    private readonly List<IGlanceAssistantSemanticResolver> resolvers = [];
    private readonly object synchronization = new();

    public GlanceAssistantSemanticResolverService(IEnumerable<IGlanceAssistantSemanticResolver> resolvers,
        IGlanceActionService actionService,
        ILogger<GlanceAssistantSemanticResolverService> logger)
    {
        this.actionService = actionService;
        this.logger = logger;
        Register(resolvers);
    }

    public IReadOnlyList<IGlanceAssistantSemanticResolver> Resolvers
    {
        get
        {
            lock (synchronization)
            {
                return [.. resolvers];
            }
        }
    }

    public IGlanceAssistantSemanticResolver? ActiveResolver
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public void Register(IEnumerable<IGlanceAssistantSemanticResolver> registrations)
    {
        bool changed = false;

        lock (synchronization)
        {
            foreach (IGlanceAssistantSemanticResolver resolver in registrations)
            {
                if (resolvers.Any(candidate => string.Equals(candidate.Id, resolver.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                resolvers.Add(resolver);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        OnPropertyChanged(nameof(Resolvers));
        ActiveResolver ??= Resolvers.FirstOrDefault();
    }

    public void Unregister(IEnumerable<IGlanceAssistantSemanticResolver> registrations)
    {
        HashSet<IGlanceAssistantSemanticResolver> removals = [.. registrations];
        bool activeRemoved;

        lock (synchronization)
        {
            _ = resolvers.RemoveAll(removals.Contains);
            activeRemoved = ActiveResolver is not null && removals.Contains(ActiveResolver);
        }

        if (activeRemoved)
        {
            ActiveResolver = Resolvers.FirstOrDefault();
        }

        OnPropertyChanged(nameof(Resolvers));
    }

    public async Task<GlanceAssistantCommandResult> TryExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        IGlanceAssistantSemanticResolver? resolver = ActiveResolver;

        if (resolver is null)
        {
            return GlanceAssistantCommandResult.NotHandled;
        }

        IReadOnlyList<GlanceActionDescriptor> actions = actionService.GetActions();

        if (actions.Count == 0)
        {
            return GlanceAssistantCommandResult.NotHandled;
        }

        try
        {
            GlanceAssistantActionResolution? resolution = await resolver.ResolveAsync(command, actions, cancellationToken);

            if (resolution is null)
            {
                return GlanceAssistantCommandResult.NotHandled;
            }

            GlanceActionDescriptor? action = actions.FirstOrDefault(candidate => string.Equals(candidate.Id, resolution.ActionId, StringComparison.OrdinalIgnoreCase));

            if (action is null)
            {
                logger.LogWarning("Assistant resolver {AssistantResolver} returned unavailable action {ActionId}", resolver.Id, resolution.ActionId);
                return GlanceAssistantCommandResult.NotHandled;
            }

            GlanceActionResult result = await actionService.InvokeAsync(new GlanceActionRequest(action.Id, resolution.Arguments), cancellationToken);
            string response = result.Message ?? resolution.Response ?? action.DisplayName;
            bool handled = result.Status is not GlanceActionStatus.InvalidArguments and not GlanceActionStatus.Unavailable;
            return new GlanceAssistantCommandResult(handled, response, result.Guidance);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Assistant resolver {AssistantResolver} failed to interpret {AssistantCommand}", resolver.Id, command);
            return GlanceAssistantCommandResult.NotHandled;
        }
    }
}
