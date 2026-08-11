namespace Glance.Shell;

public sealed class ModuleSettingsCategoryViewModel :
    SettingsCategoryViewModel
{
    public ModuleSettingsCategoryViewModel(string id,
        string title,
        string glyph,
        IEnumerable<object> items,
        ModulesViewModel modules) :
        base(id, title, glyph, items) => Modules = modules;

    public ModulesViewModel Modules { get; }
}
