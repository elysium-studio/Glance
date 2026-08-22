namespace Glance.Settings;

public sealed class GlanceSettingsMigrationException :
    GlanceSettingsException
{
    public GlanceSettingsMigrationException(string message) :
        base(message)
    {
    }

    public GlanceSettingsMigrationException(string message, Exception innerException) :
        base(message, innerException)
    {
    }
}
