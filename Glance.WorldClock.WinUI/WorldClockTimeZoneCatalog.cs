using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Glance.WorldClock.WinUI;

public sealed partial record WorldClockTimeZoneOption(string Id,
    string DisplayName);

internal static class WorldClockTimeZoneCatalog
{
    private static readonly IReadOnlyList<TimeZoneInfo> SystemTimeZones = TimeZoneInfo.GetSystemTimeZones();
    private static readonly IReadOnlyList<WorldClockTimeZoneOption> AvailableTimeZones =
        [.. SystemTimeZones
            .Where(timeZone => !string.Equals(timeZone.Id, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(timeZone => timeZone.BaseUtcOffset)
            .ThenBy(timeZone => timeZone.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(timeZone => new WorldClockTimeZoneOption(timeZone.Id, timeZone.DisplayName))];

    public static IReadOnlyList<WorldClockTimeZoneOption> GetAvailableTimeZones() => AvailableTimeZones;

    public static IEnumerable<WorldClockDefinition> CreateDefinitions(WorldClockSettings settings,
        ITextLocalizer localizer)
    {
        yield return new WorldClockDefinition("Local", localizer.GetText("LocalClock"), TimeZoneInfo.Local);
        HashSet<string> addedIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (string id in settings.TimeZoneIds ?? [])
        {
            TimeZoneInfo? timeZone = FindTimeZone(id);

            if (timeZone is not null &&
                !string.Equals(timeZone.Id, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase) &&
                addedIds.Add(timeZone.Id))
            {
                yield return new WorldClockDefinition(timeZone.Id, GetFriendlyName(timeZone), timeZone);
            }
        }
    }

    public static string GetFriendlyName(TimeZoneInfo timeZone)
    {
        string displayName = timeZone.DisplayName;
        int offsetEnd = displayName.IndexOf(')');
        return offsetEnd >= 0 && offsetEnd + 1 < displayName.Length
            ? displayName[(offsetEnd + 1)..].Trim()
            : displayName;
    }

    public static bool TryCreateDefinition(string query,
        out WorldClockDefinition? definition) =>
        ResolveDefinition(query, out definition) == WorldClockDefinitionResolution.Resolved;

    public static WorldClockDefinitionResolution ResolveDefinition(string query,
        out WorldClockDefinition? definition)
    {
        string normalizedQuery = Normalize(query);
        var candidates = SystemTimeZones
            .Select(timeZone => (TimeZone: timeZone, Score: GetMatchScore(timeZone, normalizedQuery)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.TimeZone.DisplayName.Length)
            .ToArray();

        if (candidates.Length == 0)
        {
            definition = null;
            return WorldClockDefinitionResolution.NotFound;
        }

        int bestScore = candidates[0].Score;
        TimeZoneInfo[] bestMatches = [.. candidates.Where(candidate => candidate.Score == bestScore).Select(candidate => candidate.TimeZone)];

        if (bestMatches.Length != 1)
        {
            definition = null;
            return WorldClockDefinitionResolution.Ambiguous;
        }

        TimeZoneInfo timeZone = bestMatches[0];
        definition = new WorldClockDefinition(timeZone.Id, GetFriendlyName(timeZone), timeZone);
        return WorldClockDefinitionResolution.Resolved;
    }

    private static int GetMatchScore(TimeZoneInfo timeZone,
        string query)
    {
        if (query.Length == 0)
        {
            return 0;
        }

        return GetSearchNames(timeZone)
            .Select(name => Normalize(name))
            .Select(name => name == query
                ? 100
                : name.EndsWith($" {query}", StringComparison.OrdinalIgnoreCase)
                    ? 90
                    : name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        ? 80
                        : query.Contains(name, StringComparison.OrdinalIgnoreCase) && name.Length >= 4
                            ? 70
                            : query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(token => name.Contains(token, StringComparison.OrdinalIgnoreCase))
                                ? 60
                                : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static IEnumerable<string> GetSearchNames(TimeZoneInfo timeZone)
    {
        yield return timeZone.Id;
        yield return timeZone.DisplayName;
        yield return GetFriendlyName(timeZone);
        yield return timeZone.StandardName;
        yield return timeZone.DaylightName;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out string? ianaId) && ianaId is not null)
        {
            yield return ianaId;
        }
    }

    private static string Normalize(string value)
    {
        char[] characters = [.. value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ')];
        return string.Join(' ', new string(characters).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static TimeZoneInfo? FindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}

internal enum WorldClockDefinitionResolution
{
    Resolved,
    NotFound,
    Ambiguous
}
