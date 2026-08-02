using Glance.Application.Abstractions;
using System.Buffers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.WorldClock.WinUI;

public sealed class WorldClockAssistantCommandHandler(IGlanceActionService actionService) :
    IGlanceAssistantCommandHandler
{
    public int Priority => 100;

    public async Task<GlanceAssistantCommandResult> TryHandleAsync(string command,
        CancellationToken cancellationToken = default)
    {
        if (!WorldClockCommandParser.TryGetLocation(command, out string location))
        {
            return GlanceAssistantCommandResult.NotHandled;
        }

        GlanceActionResult result = await actionService.InvokeAsync(CreateRequest(location), cancellationToken);
        return new GlanceAssistantCommandResult(true, result.Message);
    }

    private static GlanceActionRequest CreateRequest(string location)
    {
        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("city", location);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return new GlanceActionRequest("WorldClock.ShowTime", document.RootElement.Clone());
    }
}
