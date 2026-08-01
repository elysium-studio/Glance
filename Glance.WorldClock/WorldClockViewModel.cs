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

    public void Refresh(DateTimeOffset utcNow, bool use24HourTime)
    {
        foreach (WorldClockItemViewModel clock in Clocks)
        {
            clock.Refresh(utcNow, use24HourTime);
        }
    }
}
