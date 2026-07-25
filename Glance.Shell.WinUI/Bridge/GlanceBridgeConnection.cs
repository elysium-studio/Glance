using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

internal sealed class GlanceBridgeConnection(
    string applicationId,
    StreamWriter writer) :
    IGlanceApplicationConnection,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public string ApplicationId { get; } = applicationId;

    public async ValueTask SendAsync(
        string capability,
        string topic,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        GlanceBridgeWireMessage message = new()
        {
            Kind = "event",
            ProtocolVersion = GlanceBridgeProtocol.Version,
            Capability = capability,
            Topic = topic,
            Payload = payload
        };

        await WriteAsync(message, cancellationToken);
    }

    public async ValueTask SendCapabilitiesAsync(
        IReadOnlyCollection<string> capabilities,
        CancellationToken cancellationToken = default)
    {
        GlanceBridgeWireMessage message = new()
        {
            Kind = "capabilities",
            ProtocolVersion = GlanceBridgeProtocol.Version,
            Capabilities = capabilities.ToArray()
        };

        await WriteAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        writeLock.Dispose();
        await writer.DisposeAsync();
    }

    private async ValueTask WriteAsync(
        GlanceBridgeWireMessage message,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(message, serializerOptions);
        await writeLock.WaitAsync(cancellationToken);

        try
        {
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
