using System.Text.RegularExpressions;

namespace Glance.WorldClock;

public static partial class WorldClockCommandParser
{
    public static bool TryGetLocation(string command,
        out string location)
    {
        Match match = TimeQueryPattern().Match(command);
        location = match.Success
            ? match.Groups["location"].Value.Trim(' ', '\t', '\r', '\n', '?', '!', '.', ',', '"', '\'')
            : string.Empty;
        return location.Length > 0;
    }

    [GeneratedRegex(@"\b(?:what(?:'s| is)?\s+(?:the\s+)?(?:current\s+)?time(?:\s+is\s+it)?|show(?:\s+me)?\s+(?:the\s+)?(?:current\s+)?time|tell(?:\s+me)?\s+(?:the\s+)?(?:current\s+)?time|(?:the\s+)?(?:current\s+)?time)\s+(?:in|for|at)\s+(?<location>.+?)[\s?!.]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeQueryPattern();
}
