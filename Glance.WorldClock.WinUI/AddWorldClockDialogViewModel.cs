using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glance.UI.WinUI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Glance.WorldClock.WinUI;

public sealed partial class AddWorldClockDialogViewModel :
    ObservableObject
{
    private readonly WorldClockLocationsSettingViewModel owner;

    public AddWorldClockDialogViewModel(WorldClockLocationsSettingViewModel owner,
        ModuleResourceTextLocalizer<WorldClockModule> localizer)
    {
        this.owner = owner;
        AvailableClocks = owner.GetAvailableClocks();
        Title = localizer.GetText("AddClock");
        AddLabel = localizer.GetText("Add");
        CancelLabel = localizer.GetText("Cancel");
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddClock))]
    public partial WorldClockTimeZoneOption? SelectedClock { get; set; }

    public IReadOnlyList<WorldClockTimeZoneOption> AvailableClocks { get; }

    public string Title { get; }

    public string AddLabel { get; }

    public string CancelLabel { get; }

    public bool CanAddClock => SelectedClock is not null;

    [RelayCommand]
    private async Task AddClockAsync()
    {
        owner.SelectedTimeZone = SelectedClock;
        await owner.AddClockAsync();
    }
}
