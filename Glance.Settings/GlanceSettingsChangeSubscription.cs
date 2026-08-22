namespace Glance.Settings;

internal sealed class GlanceSettingsChangeSubscription(Action unsubscribe) :
    IDisposable
{
    private Action? unsubscribe = unsubscribe;

    public void Dispose() => Interlocked.Exchange(ref unsubscribe, null)?.Invoke();
}
