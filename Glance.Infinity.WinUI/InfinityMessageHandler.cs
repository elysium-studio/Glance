using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Infinity.WinUI;

public sealed class InfinityMessageHandler(InfinityViewModel viewModel, InfinityBridgeClient bridgeClient, IDispatcher dispatcher, IGlanceAttentionService attentionService) :
    IGlanceApplicationMessageHandler
{
    private const int InteractionHandoffDurationMs = 3000;
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
    private bool attentionPending;
    private int visibilityGeneration;

    public const string PagesCapability = "infinity.pages.v1";
    public const string PageNavigationTopic = "page-navigation";
    public const string PageNavigationVisibilityTopic = "page-navigation-visibility";
    public const string PageTitleUpdateTopic = "page-title-update";

    public string ApplicationId => "ElysiumStudio.Infinity";

    public string ComponentId => "Infinity";

    public IReadOnlyCollection<string> Capabilities { get; } = [PagesCapability];

    public ValueTask ConnectedAsync(IGlanceApplicationConnection connection, CancellationToken cancellationToken)
    {
        bridgeClient.Connect(connection);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(GlanceApplicationMessage message, IGlanceApplicationConnection connection, CancellationToken cancellationToken)
    {
        bridgeClient.Connect(connection);

        if (string.Equals(message.Topic, PageNavigationVisibilityTopic, StringComparison.OrdinalIgnoreCase))
        {
            InfinityPageNavigationVisibility? visibility = message.Payload.Deserialize<InfinityPageNavigationVisibility>(serializerOptions);

            if (visibility is not null)
            {
                int generation = Interlocked.Increment(ref visibilityGeneration);

                if (visibility.IsVisible)
                {
                    dispatcher.Dispatch(() =>
                    {
                        attentionPending = true;
                        viewModel.SetSurfaceVisibility(true);
                    });
                }
                else
                {
                    dispatcher.Dispatch(() => viewModel.SetSurfaceVisibility(false));
                    _ = DismissAfterInteractionHandoffAsync(generation);
                }
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
            bool shouldRequestAttention = viewModel.IsAvailable && (attentionPending || !viewModel.IsConnected || viewModel.PageNumber != state.PageNumber);
            attentionPending = false;
            viewModel.Update(state);

            if (shouldRequestAttention)
            {
                dispatcher.Dispatch(() => attentionService.RequestAttention(ComponentId));
            }
        });

        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectedAsync(IGlanceApplicationConnection connection, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref visibilityGeneration);
        bridgeClient.Disconnect(connection);
        dispatcher.Dispatch(() =>
        {
            attentionPending = false;
            viewModel.Disconnect();
        });

        return ValueTask.CompletedTask;
    }

    private async Task DismissAfterInteractionHandoffAsync(int generation)
    {
        await Task.Delay(InteractionHandoffDurationMs);

        if (generation == Volatile.Read(ref visibilityGeneration))
        {
            dispatcher.Dispatch(viewModel.DismissIfIdle);
        }
    }
}
