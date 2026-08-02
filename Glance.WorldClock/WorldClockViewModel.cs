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

    public bool SelectClock(string query)
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

        if (clock is null)
        {
            return false;
        }

        SelectedClock = clock;
        return true;
    }

    public void Refresh(DateTimeOffset utcNow, bool use24HourTime)
    {
        foreach (WorldClockItemViewModel clock in Clocks)
        {
            clock.Refresh(utcNow, use24HourTime);
        }
    }
}
