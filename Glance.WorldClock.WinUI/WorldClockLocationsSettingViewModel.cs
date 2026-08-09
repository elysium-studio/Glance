using CommunityToolkit.Mvvm.ComponentModel;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockLocationsSettingViewModel(WorldClockSettings settings,
    IWritableOptions<WorldClockSettings> writer) :
    ObservableObject,
    IGlanceModuleSettingViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddClock))]
    public partial WorldClockTimeZoneOption? SelectedTimeZone { get; set; }

    public string ModuleId => "WorldClock";

    public int Order => 20;

    public IReadOnlyList<WorldClockTimeZoneOption> AvailableTimeZones { get; } = WorldClockTimeZoneCatalog.GetAvailableTimeZones();

    public ObservableCollection<WorldClockTimeZoneOption> Clocks { get; } = [.. (settings.TimeZoneIds ?? [])
        .Select(id => WorldClockTimeZoneCatalog.GetAvailableTimeZones().FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase)))
        .OfType<WorldClockTimeZoneOption>()
        .DistinctBy(option => option.Id, StringComparer.OrdinalIgnoreCase)];

    public bool HasClocks => Clocks.Count > 0;

    public bool CanAddClock => SelectedTimeZone is not null && !Clocks.Any(clock => string.Equals(clock.Id, SelectedTimeZone.Id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<WorldClockTimeZoneOption> GetAvailableClocks() => [.. AvailableTimeZones
        .Where(option => !Clocks.Any(clock => string.Equals(clock.Id, option.Id, StringComparison.OrdinalIgnoreCase)))];

    public async Task AddClockAsync()
    {
        WorldClockTimeZoneOption? clock = SelectedTimeZone;

        if (clock is null || !CanAddClock)
        {
            return;
        }

        Clocks.Add(clock);
        SelectedTimeZone = null;
        NotifyClocksChanged();
        await SaveAsync();
    }

    public async Task RemoveClockAsync(WorldClockTimeZoneOption clock)
    {
        if (Clocks.Remove(clock))
        {
            NotifyClocksChanged();
            await SaveAsync();
        }
    }

    public async Task MoveClockAsync(WorldClockTimeZoneOption clock,
        int offset)
    {
        int currentIndex = Clocks.IndexOf(clock);
        int targetIndex = currentIndex + offset;

        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= Clocks.Count)
        {
            return;
        }

        Clocks.Move(currentIndex, targetIndex);
        await SaveAsync();
    }

    public void Dispose()
    {
    }

    private Task SaveAsync()
    {
        string[] ids = [.. Clocks.Select(clock => clock.Id)];
        return writer.WriteAsync(options => options.TimeZoneIds = [.. ids]);
    }

    private void NotifyClocksChanged()
    {
        OnPropertyChanged(nameof(HasClocks));
        OnPropertyChanged(nameof(CanAddClock));
    }
}
