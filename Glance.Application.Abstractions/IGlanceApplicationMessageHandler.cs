using System.Text.Json;

namespace Glance.Application.Abstractions;

public interface IGlanceApplicationMessageHandler
{
    string ApplicationId { get; }

    string ComponentId { get; }

    IReadOnlyCollection<string> Capabilities { get; }

    ValueTask ConnectedAsync(
        IGlanceApplicationConnection connection,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    ValueTask HandleAsync(
        GlanceApplicationMessage message,
        IGlanceApplicationConnection connection,
        CancellationToken cancellationToken);

    ValueTask DisconnectedAsync(
        IGlanceApplicationConnection connection,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}

public interface IGlanceApplicationConnection
{
    string ApplicationId { get; }

    ValueTask SendAsync(
        string capability,
        string topic,
        JsonElement payload,
        CancellationToken cancellationToken = default);
}

public sealed record GlanceApplicationMessage(
    string Capability,
    string Topic,
    JsonElement Payload);
