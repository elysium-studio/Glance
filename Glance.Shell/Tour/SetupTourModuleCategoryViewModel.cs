using System.Collections.ObjectModel;

namespace Glance.Shell;

public sealed class SetupTourModuleCategoryViewModel :
    ObservableCollection<SetupTourModuleViewModel>
{
    public SetupTourModuleCategoryViewModel(string id, string displayName, string glyph, int order, IEnumerable<SetupTourModuleViewModel> modules) :
        base(modules)
    {
        Id = id;
        DisplayName = displayName;
        Glyph = glyph;
        Order = order;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Glyph { get; }

    public int Order { get; }
}
