using Glance.Application.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureAssistantCommandHandler(ScreenCaptureViewModel viewModel) :
    IGlanceAssistantCommandHandler
{
    public int Priority => 100;

    public Task<GlanceAssistantCommandResult> TryHandleAsync(string command, CancellationToken cancellationToken = default)
    {
        string normalizedCommand = command.Trim().ToLowerInvariant();

        if (!normalizedCommand.Contains("screenshot") &&
            !normalizedCommand.Contains("screen shot") &&
            !normalizedCommand.Contains("screen capture"))
        {
            return Task.FromResult(GlanceAssistantCommandResult.NotHandled);
        }

        if (normalizedCommand.Contains("region") || normalizedCommand.Contains("area") || normalizedCommand.Contains("selection"))
        {
            viewModel.CaptureRegion();
            return Handled("Select the region to capture");
        }

        if (normalizedCommand.Contains("full screen") || normalizedCommand.Contains("desktop") || normalizedCommand.Contains("display"))
        {
            viewModel.CaptureDisplay();
            return Handled("Capturing the full screen");
        }

        viewModel.CaptureWindow();
        return Handled("Choose the window to capture");
    }

    private static Task<GlanceAssistantCommandResult> Handled(string response) =>
        Task.FromResult(new GlanceAssistantCommandResult(true, response));
}
