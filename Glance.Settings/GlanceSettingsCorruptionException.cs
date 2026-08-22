namespace Glance.Settings;

internal sealed class GlanceSettingsCorruptionException(string message) :
    GlanceSettingsException(message)
{
}
