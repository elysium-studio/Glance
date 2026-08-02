using Glance.Application.Abstractions;
using System;
using System.Buffers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureAssistantCommandHandler(IGlanceActionService actionService) :
    IGlanceAssistantCommandHandler
{
    private const int AcknowledgementDurationMilliseconds = 650;

    public int Priority => 100;

    public async Task<GlanceAssistantCommandResult> TryHandleAsync(string command,
        CancellationToken cancellationToken = default)
    {
        string normalizedCommand = command.Trim().ToLowerInvariant();

        if (!normalizedCommand.Contains("screenshot") &&
            !normalizedCommand.Contains("screen shot") &&
            !normalizedCommand.Contains("screen capture"))
        {
            return GlanceAssistantCommandResult.NotHandled;
        }

        GlanceActionRequest request;
        string response;

        if (normalizedCommand.Contains("region") || normalizedCommand.Contains("area") || normalizedCommand.Contains("selection"))
        {
            request = new GlanceActionRequest("ScreenCapture.Region");
            response = "Select the region to capture";
        }
        else if (normalizedCommand.Contains("all displays") || normalizedCommand.Contains("all screens") || normalizedCommand.Contains("entire desktop"))
        {
            request = new GlanceActionRequest("ScreenCapture.AllDisplays");
            response = "Capturing all displays";
        }
        else if (normalizedCommand.Contains("full screen") || normalizedCommand.Contains("desktop") || normalizedCommand.Contains("display"))
        {
            request = new GlanceActionRequest("ScreenCapture.Display");
            response = "Choose the display to capture";
        }
        else
        {
            request = CreateWindowRequest(TryGetWindowName(command));
            response = "Choose the window to capture";
        }

        await Task.Delay(AcknowledgementDurationMilliseconds, cancellationToken);
        GlanceActionResult result = await actionService.InvokeAsync(request, cancellationToken);
        return new GlanceAssistantCommandResult(true, result.Succeeded ? response : result.Message ?? "The capture was cancelled");
    }

    private static string? TryGetWindowName(string command)
    {
        int windowIndex = command.IndexOf("window", StringComparison.OrdinalIgnoreCase);
        int separatorIndex = command.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);

        if (windowIndex < 0 || separatorIndex < 0 || separatorIndex <= windowIndex)
        {
            return null;
        }

        string windowName = command[(separatorIndex + 4)..].Trim().TrimEnd('.', '?', '!');
        return string.IsNullOrWhiteSpace(windowName) ? null : windowName;
    }

    private static GlanceActionRequest CreateWindowRequest(string? windowName)
    {
        if (string.IsNullOrWhiteSpace(windowName))
        {
            return new GlanceActionRequest("ScreenCapture.Window");
        }

        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("window", windowName);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return new GlanceActionRequest("ScreenCapture.Window", document.RootElement.Clone());
    }
}
