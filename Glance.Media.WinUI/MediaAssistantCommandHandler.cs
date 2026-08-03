using Glance.Application.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Media.WinUI;

public sealed class MediaAssistantCommandHandler(MediaViewModel viewModel) :
    IGlanceAssistantCommandHandler
{
    public int Priority => 100;

    public Task<GlanceAssistantCommandResult> TryHandleAsync(string command, CancellationToken cancellationToken = default)
    {
        string normalizedCommand = command.Trim().ToLowerInvariant();

        if (ContainsAny(normalizedCommand, "skip track", "next track", "next song", "skip song"))
        {
            if (viewModel.HasSession && viewModel.CanSkipNext)
            {
                viewModel.Next();
                return Handled("Skipping to the next track");
            }

            return Handled("The current media app cannot skip tracks");
        }

        if (ContainsAny(normalizedCommand, "previous track", "previous song", "go back a track", "last track"))
        {
            if (viewModel.HasSession && viewModel.CanSkipPrevious)
            {
                viewModel.Previous();
                return Handled("Going back one track");
            }

            return Handled("The current media app cannot go back a track");
        }

        if (ContainsAny(normalizedCommand, "pause music", "pause media", "pause track", "stop playback"))
        {
            if (viewModel.HasSession && viewModel.CanTogglePlayback && viewModel.IsPlaying)
            {
                viewModel.TogglePlayback();
                return Handled("Pausing playback");
            }

            return Handled(viewModel.HasSession ? "Playback is already paused" : "There is no controllable media playing");
        }

        if (ContainsAny(normalizedCommand, "play music", "play media", "resume music", "resume media", "continue playback"))
        {
            if (viewModel.HasSession && viewModel.CanTogglePlayback && !viewModel.IsPlaying)
            {
                viewModel.TogglePlayback();
                return Handled("Resuming playback");
            }

            return Handled(viewModel.HasSession ? "Playback is already running" : "There is no controllable media playing");
        }

        return Task.FromResult(GlanceAssistantCommandResult.NotHandled);
    }

    private static bool ContainsAny(string command, params string[] phrases) =>
        phrases.Any(command.Contains);

    private static Task<GlanceAssistantCommandResult> Handled(string response) =>
        Task.FromResult(new GlanceAssistantCommandResult(true, response));
}
