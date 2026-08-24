using Glance.Application.Abstractions;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.Timer.WinUI;

public sealed partial class TimerAssistantCommandHandler(TimerViewModel viewModel,
    IGlanceAttentionService attentionService) :
    IGlanceAssistantCommandHandler
{
    public int Priority => 100;

    public Task<GlanceAssistantCommandResult> TryHandleAsync(string command, CancellationToken cancellationToken = default)
    {
        Match match = TimerCommandPattern().Match(command);

        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out double value))
        {
            return Task.FromResult(GlanceAssistantCommandResult.NotHandled);
        }

        TimeSpan duration = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "hour" or "hours" => TimeSpan.FromHours(Math.Clamp(value, 1d / 3600, 24)),
            "second" or "seconds" or "sec" or "secs" => TimeSpan.FromSeconds(Math.Clamp(value, 1, 86400)),
            _ => TimeSpan.FromMinutes(Math.Clamp(value, 1d / 60, 1440))
        };

        viewModel.Start(duration);
        attentionService.RequestAttention("Timer");
        return Task.FromResult(new GlanceAssistantCommandResult(true, $"Timer set for {FormatDuration(duration)}"));
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
            ? $"{duration.TotalHours:0.#} hours"
            : duration.TotalMinutes >= 1
                ? $"{duration.TotalMinutes:0.#} minutes"
                : $"{duration.TotalSeconds:0.#} seconds";

    [GeneratedRegex(@"(?:set|start|create)?\s*(?:a\s+)?timer(?:\s+for)?\s+(\d+(?:\.\d+)?)\s*(hours?|minutes?|mins?|seconds?|secs?)", RegexOptions.IgnoreCase)]
    private static partial Regex TimerCommandPattern();
}
