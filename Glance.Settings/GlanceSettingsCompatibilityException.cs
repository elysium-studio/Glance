namespace Glance.Settings;

public sealed class GlanceSettingsCompatibilityException(string message) :
    GlanceSettingsException(message)
{
}
