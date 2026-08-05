using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Glance.Shell.WinUI;

internal sealed class GlanceBridgeConnection :
    IGlanceApplicationConnection,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource disposalCancellation = new();
    private readonly Channel<GlanceBridgeWireMessage> messages = Channel.CreateUnbounded<GlanceBridgeWireMessage>(new UnboundedChannelOptions
    {
        SingleReader = true
    });
    private readonly StreamWriter writer;
    private readonly Task writerTask;
    private int disposed;

    public GlanceBridgeConnection(string applicationId, StreamWriter writer)
    {
        ApplicationId = applicationId;
        this.writer = writer;
        writerTask = WriteMessagesAsync();
    }

    public string ApplicationId { get; }

    public async ValueTask SendAsync(string capability, string topic, JsonElement payload, CancellationToken cancellationToken = default)
    {
        GlanceBridgeWireMessage message = new()
        {
            Kind = "event",
            ProtocolVersion = GlanceBridgeProtocol.Version,
            Capability = capability,
            Topic = topic,
            Payload = payload
        };

        await QueueAsync(message, cancellationToken);
    }

    public async ValueTask SendCapabilitiesAsync(IReadOnlyCollection<string> capabilities, CancellationToken cancellationToken = default)
    {
        GlanceBridgeWireMessage message = new()
        {
            Kind = "capabilities",
            ProtocolVersion = GlanceBridgeProtocol.Version,
            Capabilities = [.. capabilities]
        };

        await QueueAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        _ = messages.Writer.TryComplete();
        disposalCancellation.Cancel();
        await writer.DisposeAsync();
        await writerTask;
        disposalCancellation.Dispose();
    }

    private ValueTask QueueAsync(GlanceBridgeWireMessage message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        return messages.Writer.WriteAsync(message, cancellationToken);
    }

    private async Task WriteMessagesAsync()
    {
        try
        {
            await foreach (GlanceBridgeWireMessage message in messages.Reader.ReadAllAsync(disposalCancellation.Token))
            {
                string json = JsonSerializer.Serialize(message, serializerOptions);
                await writer.WriteLineAsync(json.AsMemory(), disposalCancellation.Token);
                await writer.FlushAsync(disposalCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (disposalCancellation.IsCancellationRequested)
        {
        }
        catch (IOException exception)
        {
            _ = messages.Writer.TryComplete(exception);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref disposed) != 0)
        {
        }
    }
}
