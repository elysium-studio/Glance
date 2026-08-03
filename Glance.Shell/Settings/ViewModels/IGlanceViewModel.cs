namespace Glance.Shell;

public interface IGlanceViewModel :
    IDisposable
{
    string SettingsCategory => GlanceSettingsCategories.AppearanceAndBehaviour;
}
