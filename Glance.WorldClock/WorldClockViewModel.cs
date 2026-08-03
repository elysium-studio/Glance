using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Glance.WorldClock;

public sealed partial class WorldClockViewModel(IEnumerable<WorldClockDefinition> clocks) :
    ObservableObject
{
    [ObservableProperty]
    private WorldClockItemViewModel? selectedClock;

    public ObservableCollection<WorldClockItemViewModel> Clocks { get; } = [.. clocks.Select(clock => new WorldClockItemViewModel(clock))];

    public WorldClockItemViewModel LocalClock => Clocks[0];

    public void Initialize() => SelectedClock = LocalClock;

    public void SetClocks(IEnumerable<WorldClockDefinition> clocks)
    {
        string? selectedId = SelectedClock?.Id;
        List<WorldClockItemViewModel> replacements = [.. clocks.Select(clock => new WorldClockItemViewModel(clock))];

        if (replacements.Count == 0)
        {
            return;
        }

        Clocks.Clear();

        foreach (WorldClockItemViewModel clock in replacements)
        {
            Clocks.Add(clock);
        }

        SelectedClock = Clocks.FirstOrDefault(clock => string.Equals(clock.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ?? LocalClock;
    }

    public bool SelectClock(string query)
    {
        WorldClockItemViewModel? clock = FindClock(query);

        if (clock is null)
        {
            return false;
        }

        SelectedClock = clock;
        return true;
    }

    public bool CanSelectClock(string query) => FindClock(query) is not null;

    private WorldClockItemViewModel? FindClock(string query)
    {
        WorldClockItemViewModel? clock = Clocks.FirstOrDefault(clock =>
            string.Equals(clock.Id, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(clock.DisplayName, query, StringComparison.OrdinalIgnoreCase));

        if (clock is null)
        {
            clock = Clocks.FirstOrDefault(clock =>
                clock.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                query.Contains(clock.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        return clock;
    }

    public void ShowClock(WorldClockDefinition definition)
    {
        WorldClockItemViewModel? clock = Clocks.FirstOrDefault(clock => string.Equals(clock.Id, definition.Id, StringComparison.OrdinalIgnoreCase));

        if (clock is null)
        {
            clock = new WorldClockItemViewModel(definition);
            Clocks.Add(clock);
        }

        SelectedClock = clock;
    }

    public void Refresh(DateTimeOffset utcNow, bool use24HourTime)
    {
        foreach (WorldClockItemViewModel clock in Clocks)
        {
            clock.Refresh(utcNow, use24HourTime);
        }
    }
}
