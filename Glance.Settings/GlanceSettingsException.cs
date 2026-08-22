namespace Glance.Settings;

public class GlanceSettingsException : Exception
{
    public GlanceSettingsException(string message) :
        base(message)
    {
    }

    public GlanceSettingsException(string message, Exception innerException) :
        base(message, innerException)
    {
    }
}
