using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Infinity.WinUI;

public sealed class InfinityMessageHandler(InfinityViewModel viewModel, IDispatcher dispatcher, IGlanceAttentionService attentionService) :
    IGlanceApplicationMessageHandler
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    public const string PagesCapability = "infinity.pages.v1";
    public const string PageNavigationTopic = "page-navigation";
    public const string PageNavigationVisibilityTopic = "page-navigation-visibility";

    public string ApplicationId => "ElysiumStudio.Infinity";

    public string ComponentId => "Infinity";

    public IReadOnlyCollection<string> Capabilities { get; } = [PagesCapability];

    public ValueTask HandleAsync(GlanceApplicationMessage message, IGlanceApplicationConnection connection, CancellationToken cancellationToken)
    {
        if (string.Equals(message.Topic, PageNavigationVisibilityTopic, StringComparison.OrdinalIgnoreCase))
        {
            InfinityPageNavigationVisibility? visibility = message.Payload.Deserialize<InfinityPageNavigationVisibility>(serializerOptions);

            if (visibility is not null)
            {
                dispatcher.Dispatch(() => viewModel.IsAvailable = visibility.IsVisible);
            }

            return ValueTask.CompletedTask;
        }

        if (!string.Equals(message.Topic, PageNavigationTopic, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        InfinityPageNavigationState? state = message.Payload.Deserialize<InfinityPageNavigationState>(serializerOptions);

        if (state is null)
        {
            return ValueTask.CompletedTask;
        }

        dispatcher.Dispatch(() =>
        {
            bool shouldRequestAttention = viewModel.IsAvailable && (!viewModel.IsConnected || viewModel.PageNumber != state.PageNumber);
            viewModel.Update(state);

            if (shouldRequestAttention)
            {
                dispatcher.Dispatch(() => attentionService.RequestAttention(ComponentId, expand: false));
            }
        });

        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectedAsync(IGlanceApplicationConnection connection, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(viewModel.Disconnect);
        return ValueTask.CompletedTask;
    }
}
