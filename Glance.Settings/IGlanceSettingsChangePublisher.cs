namespace Glance.Settings;

internal interface IGlanceSettingsChangePublisher<TOptions>
    where TOptions : class, new()
{
    void Publish(TOptions settings);
}
