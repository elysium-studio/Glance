using Glance.Application.Abstractions;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Infinity.WinUI;

public sealed class InfinityBridgeClient :
    IInfinityPageTitleUpdater
{
    private IGlanceApplicationConnection? connection;

    public void Connect(IGlanceApplicationConnection connection) => this.connection = connection;

    public void Disconnect(IGlanceApplicationConnection connection)
    {
        if (ReferenceEquals(this.connection, connection))
        {
            this.connection = null;
        }
    }

    public async ValueTask<bool> UpdatePageTitleAsync(int pageIndex, string pageTitle, CancellationToken cancellationToken = default)
    {
        IGlanceApplicationConnection? currentConnection = connection;

        if (currentConnection is null)
        {
            return false;
        }

        InfinityPageTitleUpdate update = new(pageIndex, pageTitle);
        JsonElement payload = JsonSerializer.SerializeToElement(update, InfinityBridgeJsonContext.Default.InfinityPageTitleUpdate);

        try
        {
            await currentConnection.SendAsync(InfinityMessageHandler.PagesCapability, InfinityMessageHandler.PageTitleUpdateTopic, payload, cancellationToken);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InfinityPageTitleUpdate))]
internal sealed partial class InfinityBridgeJsonContext :
    JsonSerializerContext
{
}
